using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Accounting;

public sealed record SupplierBillDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    string BillNumber,
    Guid? SourceReceiveId,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    string Status,
    DateTimeOffset IssuedAt,
    string? Notes);

public sealed record SupplierPaymentAllocationInput(Guid BillId, decimal Amount);

public sealed record RecordSupplierPaymentRequest(
    Guid SupplierId,
    Guid? BranchId,
    decimal Amount,
    string MethodCode,
    string IdempotencyKey,
    DateTimeOffset? PaidAt,
    string? Notes,
    IReadOnlyList<SupplierPaymentAllocationInput> Allocations);

public sealed record SupplierPaymentDto(
    Guid Id,
    Guid SupplierId,
    decimal Amount,
    string MethodCode,
    DateTimeOffset PaidAt,
    string IdempotencyKey,
    IReadOnlyList<SupplierPaymentAllocationInput> Allocations);

public sealed class RecordSupplierPaymentValidator : AbstractValidator<RecordSupplierPaymentRequest>
{
    public RecordSupplierPaymentValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.MethodCode).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Allocations).NotEmpty();
    }
}

public interface IApService
{
    Task<Result<PagedResult<SupplierBillDto>>> ListBillsAsync(PagedQuery query, Guid? supplierId, bool? unpaidOnly, CancellationToken ct = default);
    Task<Result<SupplierBillDto>> GetBillAsync(Guid id, CancellationToken ct = default);
    Task<Result<SupplierPaymentDto>> RecordPaymentAsync(RecordSupplierPaymentRequest request, CancellationToken ct = default);
}

public sealed class ApService : IApService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IAccountingPostingService _posting;
    private readonly IValidator<RecordSupplierPaymentRequest> _validator;

    public ApService(
        IApplicationDbContext db,
        ICurrentUser user,
        IAuditService audit,
        IAccountingPostingService posting,
        IValidator<RecordSupplierPaymentRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _posting = posting;
        _validator = validator;
    }

    public async Task<Result<PagedResult<SupplierBillDto>>> ListBillsAsync(
        PagedQuery query, Guid? supplierId, bool? unpaidOnly, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<PagedResult<SupplierBillDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q =
            from b in _db.SupplierBills.AsNoTracking()
            join s in _db.Suppliers.AsNoTracking() on b.SupplierId equals s.Id
            where b.OrganizationId == orgId
            select new { b, SupplierName = s.Name };

        if (supplierId.HasValue) q = q.Where(x => x.b.SupplierId == supplierId);
        if (unpaidOnly == true) q = q.Where(x => x.b.BalanceDue > 0 && x.b.Status != SupplierBillStatuses.Voided);

        q = q.OrderByDescending(x => x.b.IssuedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(x => new SupplierBillDto(
                x.b.Id, x.b.SupplierId, x.SupplierName, x.b.BillNumber, x.b.SourceReceiveId,
                x.b.TotalAmount, x.b.AmountPaid, x.b.BalanceDue, x.b.Status, x.b.IssuedAt, x.b.Notes))
            .ToListAsync(ct);

        return Result<PagedResult<SupplierBillDto>>.Success(new PagedResult<SupplierBillDto>
        {
            Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = total
        });
    }

    public async Task<Result<SupplierBillDto>> GetBillAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<SupplierBillDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var row = await (
            from b in _db.SupplierBills.AsNoTracking()
            join s in _db.Suppliers.AsNoTracking() on b.SupplierId equals s.Id
            where b.Id == id && b.OrganizationId == orgId
            select new { b, s.Name }).FirstOrDefaultAsync(ct);
        if (row is null) return Result<SupplierBillDto>.Failure(ErrorCodes.NotFound, "Bill not found.");
        return Result<SupplierBillDto>.Success(new SupplierBillDto(
            row.b.Id, row.b.SupplierId, row.Name, row.b.BillNumber, row.b.SourceReceiveId,
            row.b.TotalAmount, row.b.AmountPaid, row.b.BalanceDue, row.b.Status, row.b.IssuedAt, row.b.Notes));
    }

    public async Task<Result<SupplierPaymentDto>> RecordPaymentAsync(RecordSupplierPaymentRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.RequireAny(_user, Permissions.AccountingAp, Permissions.AccountingPost);
        if (!auth.IsSuccess) return Result<SupplierPaymentDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<SupplierPaymentDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var key = request.IdempotencyKey.Trim();
        var existing = await _db.SupplierPayments.AsNoTracking()
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == key, ct);
        if (existing is not null)
            return Result<SupplierPaymentDto>.Success(ToPaymentDto(existing));

        if (Math.Round(request.Allocations.Sum(a => a.Amount), 2) != Math.Round(request.Amount, 2))
            return Result<SupplierPaymentDto>.Failure(ErrorCodes.Validation, "Allocation total must equal payment amount.");

        var branchId = request.BranchId ?? _user.BranchId;
        if (branchId is null)
            return Result<SupplierPaymentDto>.Failure(ErrorCodes.Validation, "Branch is required.");

        var supplierOk = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.OrganizationId == orgId, ct);
        if (!supplierOk) return Result<SupplierPaymentDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");

        var billIds = request.Allocations.Select(a => a.BillId).Distinct().ToList();
        var bills = await _db.SupplierBills
            .Where(b => b.OrganizationId == orgId && billIds.Contains(b.Id) && b.SupplierId == request.SupplierId)
            .ToListAsync(ct);
        if (bills.Count != billIds.Count)
            return Result<SupplierPaymentDto>.Failure(ErrorCodes.Validation, "One or more bills are invalid.");

        foreach (var alloc in request.Allocations)
        {
            var bill = bills.First(b => b.Id == alloc.BillId);
            if (alloc.Amount <= 0 || alloc.Amount > bill.BalanceDue + 0.0001m)
                return Result<SupplierPaymentDto>.Failure(ErrorCodes.Validation, $"Invalid allocation for bill {bill.BillNumber}.");
        }

        var maps = await AccountingLineBuilders.LoadMapsAsync(_db, orgId, ct);
        var lines = AccountingLineBuilders.SupplierPayment(maps, request.Amount, request.MethodCode);
        var paidAt = request.PaidAt ?? DateTimeOffset.UtcNow;

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            var payment = new SupplierPayment
            {
                OrganizationId = orgId,
                BranchId = branchId.Value,
                SupplierId = request.SupplierId,
                Amount = request.Amount,
                MethodCode = request.MethodCode,
                PaidAt = paidAt,
                IdempotencyKey = key,
                CreatedByUserId = _user.UserId!.Value,
                Notes = request.Notes
            };
            foreach (var alloc in request.Allocations)
            {
                payment.Allocations.Add(new SupplierPaymentAllocation
                {
                    SupplierBillId = alloc.BillId,
                    Amount = alloc.Amount
                });
                var bill = bills.First(b => b.Id == alloc.BillId);
                bill.AmountPaid += alloc.Amount;
                bill.BalanceDue = Math.Max(0, bill.TotalAmount - bill.AmountPaid);
                bill.Status = bill.BalanceDue == 0 ? SupplierBillStatuses.Paid
                    : bill.AmountPaid > 0 ? SupplierBillStatuses.Partial : SupplierBillStatuses.Open;
                bill.UpdatedAt = DateTimeOffset.UtcNow;
            }

            _db.SupplierPayments.Add(payment);
            await _db.SaveChangesAsync(ct);

            var journal = await _posting.PostAsync(new PostJournalRequest(
                orgId, branchId, paidAt, AccountingSourceTypes.SupplierPayment, payment.Id,
                $"Supplier payment {payment.Id}", lines), ct);
            if (!journal.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.Failure(journal.ErrorCode!, journal.ErrorMessage!);
            }

            await tx.CommitAsync(ct);
            await _audit.WriteAsync("payment", "SupplierPayment", payment.Id.ToString(), null,
                new { payment.Amount, payment.SupplierId }, ct);
            return Result<SupplierPaymentDto>.Success(ToPaymentDto(payment));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static SupplierPaymentDto ToPaymentDto(SupplierPayment p) =>
        new(p.Id, p.SupplierId, p.Amount, p.MethodCode, p.PaidAt, p.IdempotencyKey,
            p.Allocations.Select(a => new SupplierPaymentAllocationInput(a.SupplierBillId, a.Amount)).ToList());
}
