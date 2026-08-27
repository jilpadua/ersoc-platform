using Ersms.Application.Common;
using Ersms.Domain.Inventory;
using Ersms.Domain.Sales;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Sales;

public sealed record SaleLineDto(Guid Id, Guid PartId, string Description, decimal Quantity, decimal UnitPrice, decimal UnitCost, decimal Discount, decimal LineTotal);
public sealed record PaymentDto(Guid Id, decimal Amount, string MethodCode, DateTimeOffset PaidAt, DateTimeOffset CreatedAt, string IdempotencyKey, string Status);
public sealed record InvoiceDto(
    Guid Id,
    Guid SaleId,
    string InvoiceNumber,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? VoidedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue);
public sealed record SaleReturnLineDto(Guid Id, Guid SaleLineId, Guid PartId, decimal Quantity);
public sealed record SaleReturnDto(
    Guid Id,
    string ReturnNumber,
    DateTimeOffset CompletedAt,
    DateTimeOffset? RefundedAt,
    decimal RefundAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SaleReturnLineDto> Lines);

public sealed record SaleListDto(
    Guid Id,
    string SaleNumber,
    Guid? CustomerId,
    string? CustomerName,
    string Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? VoidedAt,
    DateTimeOffset CreatedAt);

public sealed record SaleDetailDto(
    Guid Id,
    string SaleNumber,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerName,
    string Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? VoidedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Notes,
    IReadOnlyList<SaleLineDto> Lines,
    IReadOnlyList<PaymentDto> Payments,
    InvoiceDto? Invoice,
    IReadOnlyList<SaleReturnDto> Returns);

public sealed record SaleLineInput(Guid PartId, decimal Quantity, decimal? UnitPrice = null, decimal Discount = 0);
public sealed record InitialPaymentInput(decimal Amount, string MethodCode, string IdempotencyKey);

public sealed record CreateSaleRequest(
    Guid? CustomerId,
    Guid? BranchId,
    string? Notes,
    IReadOnlyList<SaleLineInput> Lines,
    InitialPaymentInput? Payment = null);

public sealed record RecordPaymentRequest(decimal Amount, string MethodCode, string IdempotencyKey);
public sealed record ReturnLineInput(Guid SaleLineId, decimal Quantity);
public sealed record CreateReturnRequest(IReadOnlyList<ReturnLineInput> Lines, decimal? RefundAmount = null, string? RefundMethodCode = null, string? IdempotencyKey = null);

public sealed record PaymentMethodDto(Guid Id, string Code, string Name);

public sealed class CreateSaleValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PartId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.Discount).GreaterThanOrEqualTo(0);
        });
    }
}

public interface ISaleService
{
    Task<Result<PagedResult<SaleListDto>>> ListAsync(PagedQuery query, string? status, bool? unpaidOnly, CancellationToken ct = default);
    Task<Result<SaleDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<SaleDetailDto>> CreateAsync(CreateSaleRequest request, CancellationToken ct = default);
    Task<Result<SaleDetailDto>> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct = default);
    Task<Result<SaleDetailDto>> CreateReturnAsync(Guid id, CreateReturnRequest request, CancellationToken ct = default);
    Task<Result<SaleDetailDto>> VoidAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<InvoiceDto>>> ListInvoicesAsync(PagedQuery query, bool unpaidOnly, CancellationToken ct = default);
    Task<Result<InvoiceDto>> GetInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PaymentMethodDto>>> ListPaymentMethodsAsync(CancellationToken ct = default);
}

public sealed class SaleService : ISaleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateSaleRequest> _validator;

    public SaleService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateSaleRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<SaleListDto>>> ListAsync(PagedQuery query, string? status, bool? unpaidOnly, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRead);
        if (!auth.IsSuccess) return Result<PagedResult<SaleListDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = from s in _db.Sales.AsNoTracking()
                where s.OrganizationId == orgId
                join c in _db.Customers.AsNoTracking() on s.CustomerId equals c.Id into cj
                from c in cj.DefaultIfEmpty()
                select new { Sale = s, CustomerName = c != null ? c.Name : null };

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Sale.Status == status);
        if (unpaidOnly == true)
            q = q.Where(x => x.Sale.BalanceDue > 0 && x.Sale.Status == SaleStatuses.Completed);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x => x.Sale.SaleNumber.ToLower().Contains(s) ||
                             (x.CustomerName != null && x.CustomerName.ToLower().Contains(s)));
        }

        q = q.OrderByDescending(x => x.Sale.CompletedAt ?? x.Sale.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(x => new SaleListDto(
                x.Sale.Id, x.Sale.SaleNumber, x.Sale.CustomerId, x.CustomerName, x.Sale.Status,
                x.Sale.TotalAmount, x.Sale.AmountPaid, x.Sale.BalanceDue, x.Sale.CompletedAt,
                x.Sale.VoidedAt, x.Sale.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<SaleListDto>>.Success(new PagedResult<SaleListDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<SaleDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRead);
        if (!auth.IsSuccess) return Result<SaleDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var detail = await LoadDetailAsync(id, ct);
        if (detail is null) return Result<SaleDetailDto>.Failure(ErrorCodes.NotFound, "Sale not found.");
        return Result<SaleDetailDto>.Success(detail);
    }

    public async Task<Result<SaleDetailDto>> CreateAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesWrite);
        if (!auth.IsSuccess) return Result<SaleDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var branchId = request.BranchId ?? _user.BranchId;
        if (branchId is null)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Branch is required.");

        if (request.CustomerId.HasValue)
        {
            var customerOk = await _db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.OrganizationId == orgId && c.IsActive, ct);
            if (!customerOk) return Result<SaleDetailDto>.Failure(ErrorCodes.NotFound, "Customer not found.");
        }

        var partIds = request.Lines.Select(l => l.PartId).Distinct().ToList();
        var parts = await _db.Parts.Where(p => p.OrganizationId == orgId && p.IsActive && partIds.Contains(p.Id)).ToListAsync(ct);
        if (parts.Count != partIds.Count)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "One or more parts are invalid.");

        var partMap = parts.ToDictionary(p => p.Id);

        if (request.Payment is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Payment.IdempotencyKey))
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment idempotency key is required.");
            if (request.Payment.Amount <= 0)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment amount must be positive.");
            var methodOk = await _db.PaymentMethods.AnyAsync(m =>
                m.OrganizationId == orgId && m.IsActive && m.Code == request.Payment.MethodCode, ct);
            if (!methodOk) return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Invalid payment method.");

            var existingPay = await _db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == request.Payment.IdempotencyKey.Trim(), ct);
            if (existingPay is not null)
                return Result<SaleDetailDto>.Success((await LoadDetailAsync(existingPay.SaleId, ct))!);
        }

        var lines = new List<SaleLine>();
        decimal subtotal = 0;
        decimal discountTotal = 0;
        foreach (var input in request.Lines)
        {
            var part = partMap[input.PartId];
            var unitPrice = input.UnitPrice ?? part.UnitPrice;
            var lineTotal = SaleWorkflow.LineTotal(input.Quantity, unitPrice, input.Discount);
            if (lineTotal < 0)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Line total cannot be negative.");
            lines.Add(new SaleLine
            {
                PartId = input.PartId,
                Description = $"{part.Sku} — {part.Name}",
                Quantity = input.Quantity,
                UnitPrice = unitPrice,
                UnitCost = part.UnitCost,
                Discount = input.Discount,
                LineTotal = lineTotal
            });
            subtotal += input.Quantity * unitPrice;
            discountTotal += input.Discount;
        }

        var total = subtotal - discountTotal;
        if (request.Payment is not null && request.Payment.Amount > total + 0.0001m)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment exceeds balance due.");

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            var onHand = await OnHandMapAsync(orgId, branchId.Value, partIds, ct);
            foreach (var group in request.Lines.GroupBy(l => l.PartId))
            {
                var qty = group.Sum(l => l.Quantity);
                var available = onHand.GetValueOrDefault(group.Key);
                var check = StockMath.ApplyAdjustment(available, -qty);
                if (!check.IsSuccess)
                {
                    await tx.RollbackAsync(ct);
                    return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, $"Insufficient stock for {partMap[group.Key].Sku}.");
                }
            }

            var now = DateTimeOffset.UtcNow;
            var sale = new Sale
            {
                OrganizationId = orgId,
                BranchId = branchId.Value,
                CustomerId = request.CustomerId,
                SaleNumber = await NextNumberAsync("SALE-", orgId, ct),
                Status = SaleStatuses.Completed,
                Subtotal = subtotal,
                DiscountTotal = discountTotal,
                TaxTotal = 0,
                TotalAmount = total,
                AmountPaid = 0,
                BalanceDue = total,
                CompletedAt = now,
                Notes = request.Notes,
                CreatedByUserId = _user.UserId!.Value,
                Lines = lines
            };

            foreach (var line in lines)
            {
                _db.StockLedgerEntries.Add(new StockLedgerEntry
                {
                    OrganizationId = orgId,
                    BranchId = branchId.Value,
                    PartId = line.PartId,
                    QuantityDelta = -line.Quantity,
                    EntryType = StockEntryTypes.Sale,
                    ReferenceType = "Sale",
                    ReferenceId = sale.Id,
                    Reason = $"Sold on {sale.SaleNumber}",
                    ActorUserId = _user.UserId!.Value
                });
            }

            sale.Invoice = new Invoice
            {
                OrganizationId = orgId,
                SaleId = sale.Id,
                InvoiceNumber = await NextNumberAsync("INV-", orgId, ct),
                Status = InvoiceStatuses.Unpaid,
                IssuedAt = now,
                DueAt = null,
                TotalAmount = total,
                AmountPaid = 0,
                BalanceDue = total
            };

            _db.Sales.Add(sale);

            if (request.Payment is not null)
            {
                if (request.Payment.Amount > total + 0.0001m)
                {
                    await tx.RollbackAsync(ct);
                    return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment exceeds balance due.");
                }

                _db.Payments.Add(new Payment
                {
                    OrganizationId = orgId,
                    BranchId = branchId.Value,
                    SaleId = sale.Id,
                    Amount = request.Payment.Amount,
                    MethodCode = request.Payment.MethodCode,
                    PaidAt = now,
                    CreatedAt = now,
                    ReceivedByUserId = _user.UserId!.Value,
                    IdempotencyKey = request.Payment.IdempotencyKey.Trim(),
                    Status = PaymentStatuses.Succeeded
                });
                sale.AmountPaid = request.Payment.Amount;
                sale.BalanceDue = Math.Max(0, total - request.Payment.Amount);
                sale.Invoice.AmountPaid = sale.AmountPaid;
                sale.Invoice.BalanceDue = sale.BalanceDue;
                sale.Invoice.Status = SaleWorkflow.InvoiceStatusFromBalances(sale.TotalAmount, sale.AmountPaid);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (request.Payment is not null)
            {
                await tx.RollbackAsync(ct);
                var existing = await _db.Payments.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == request.Payment.IdempotencyKey.Trim(), ct);
                if (existing is not null && existing.SaleId != Guid.Empty)
                {
                    var detail = await LoadDetailAsync(existing.SaleId, ct);
                    if (detail is not null)
                        return Result<SaleDetailDto>.Success(detail);
                }
                return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Payment idempotency conflict.");
            }

            await tx.CommitAsync(ct);
            await _audit.WriteAsync("create", "Sale", sale.Id.ToString(), null, new { sale.SaleNumber, sale.TotalAmount }, ct);
            return Result<SaleDetailDto>.Success((await LoadDetailAsync(sale.Id, ct))!);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<SaleDetailDto>> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesWrite);
        if (!auth.IsSuccess) return Result<SaleDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Idempotency key is required.");
        if (request.Amount <= 0)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment amount must be positive.");

        var orgId = _user.OrganizationId!.Value;
        var key = request.IdempotencyKey.Trim();
        var existing = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.SaleId != id)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Idempotency key already used for another sale.");
            return Result<SaleDetailDto>.Success((await LoadDetailAsync(id, ct))!);
        }

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            var sale = await _db.Sales.Include(s => s.Invoice)
                .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
            if (sale is null)
            {
                await tx.RollbackAsync(ct);
                return Result<SaleDetailDto>.Failure(ErrorCodes.NotFound, "Sale not found.");
            }

            var can = SaleWorkflow.CanPay(sale.Status);
            if (!can.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result<SaleDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);
            }

            var amountPaid = await _db.Payments.Where(p => p.SaleId == sale.Id).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
            sale.AmountPaid = amountPaid;
            sale.BalanceDue = Math.Max(0, sale.TotalAmount - amountPaid);

            var methodOk = await _db.PaymentMethods.AnyAsync(m =>
                m.OrganizationId == orgId && m.IsActive && m.Code == request.MethodCode, ct);
            if (!methodOk)
            {
                await tx.RollbackAsync(ct);
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Invalid payment method.");
            }

            if (request.Amount > sale.BalanceDue + 0.0001m)
            {
                await tx.RollbackAsync(ct);
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Payment exceeds balance due.");
            }

            _db.Payments.Add(new Payment
            {
                OrganizationId = orgId,
                BranchId = sale.BranchId,
                SaleId = sale.Id,
                Amount = request.Amount,
                MethodCode = request.MethodCode,
                PaidAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                ReceivedByUserId = _user.UserId!.Value,
                IdempotencyKey = key,
                Status = PaymentStatuses.Succeeded
            });

            sale.AmountPaid = amountPaid + request.Amount;
            sale.BalanceDue = Math.Max(0, sale.TotalAmount - sale.AmountPaid);
            sale.UpdatedAt = DateTimeOffset.UtcNow;
            if (sale.Invoice is not null)
            {
                sale.Invoice.AmountPaid = sale.AmountPaid;
                sale.Invoice.BalanceDue = sale.BalanceDue;
                sale.Invoice.Status = SaleWorkflow.InvoiceStatusFromBalances(sale.TotalAmount, sale.AmountPaid);
                sale.Invoice.UpdatedAt = DateTimeOffset.UtcNow;
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                var raced = await _db.Payments.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == key, ct);
                if (raced is not null && raced.SaleId == id)
                    return Result<SaleDetailDto>.Success((await LoadDetailAsync(id, ct))!);
                return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Payment idempotency conflict.");
            }

            await tx.CommitAsync(ct);
            await _audit.WriteAsync("payment", "Sale", sale.Id.ToString(), null, new { request.Amount, request.MethodCode }, ct);
            return Result<SaleDetailDto>.Success((await LoadDetailAsync(sale.Id, ct))!);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<SaleDetailDto>> CreateReturnAsync(Guid id, CreateReturnRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRefund);
        if (!auth.IsSuccess) return Result<SaleDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        if (request.Lines.Count == 0)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "At least one return line is required.");

        var orgId = _user.OrganizationId!.Value;
        var sale = await _db.Sales
            .Include(s => s.Lines)
            .Include(s => s.Invoice)
            .Include(s => s.Returns).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (sale is null) return Result<SaleDetailDto>.Failure(ErrorCodes.NotFound, "Sale not found.");

        var can = SaleWorkflow.CanReturn(sale.Status);
        if (!can.IsSuccess) return Result<SaleDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);

        var alreadyReturned = sale.Returns
            .SelectMany(r => r.Lines)
            .GroupBy(l => l.SaleLineId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var aggregated = request.Lines
            .GroupBy(l => l.SaleLineId)
            .Select(g => new { SaleLineId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var returnNow = DateTimeOffset.UtcNow;
        var returnDoc = new SaleReturn
        {
            OrganizationId = orgId,
            BranchId = sale.BranchId,
            SaleId = sale.Id,
            ReturnNumber = await NextNumberAsync("RET-", orgId, ct),
            CompletedAt = returnNow,
            CreatedByUserId = _user.UserId!.Value
        };

        decimal refundCalc = 0;
        foreach (var input in aggregated)
        {
            if (input.Quantity <= 0)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Return quantity must be positive.");
            var line = sale.Lines.FirstOrDefault(l => l.Id == input.SaleLineId);
            if (line is null)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Invalid sale line.");
            var returned = alreadyReturned.GetValueOrDefault(line.Id);
            if (input.Quantity > line.Quantity - returned)
                return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Cannot return more than remaining sold quantity.");

            returnDoc.Lines.Add(new SaleReturnLine
            {
                SaleLineId = line.Id,
                PartId = line.PartId,
                Quantity = input.Quantity
            });
            refundCalc += Math.Round(line.LineTotal / line.Quantity * input.Quantity, 2);

            _db.StockLedgerEntries.Add(new StockLedgerEntry
            {
                OrganizationId = orgId,
                BranchId = sale.BranchId,
                PartId = line.PartId,
                QuantityDelta = input.Quantity,
                EntryType = StockEntryTypes.SaleReturn,
                ReferenceType = "SaleReturn",
                ReferenceId = returnDoc.Id,
                Reason = $"Return {returnDoc.ReturnNumber}",
                ActorUserId = _user.UserId!.Value
            });
        }

        var amountPaid = await _db.Payments.Where(p => p.SaleId == sale.Id).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        sale.AmountPaid = amountPaid;
        sale.BalanceDue = Math.Max(0, sale.TotalAmount - amountPaid);

        var refundAmount = request.RefundAmount ?? refundCalc;
        if (refundAmount < 0)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Refund amount cannot be negative.");
        if (refundAmount > sale.AmountPaid)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Validation, "Refund cannot exceed amount paid.");

        returnDoc.RefundAmount = refundAmount;
        _db.SaleReturns.Add(returnDoc);

        sale.TotalAmount = Math.Max(0, sale.TotalAmount - refundCalc);
        sale.Subtotal = Math.Max(0, sale.Subtotal - refundCalc);

        if (refundAmount > 0)
        {
            var method = request.RefundMethodCode ?? "CASH";
            var key = (request.IdempotencyKey ?? $"refund-{returnDoc.Id}").Trim();
            var existing = await _db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                if (existing.SaleId != sale.Id)
                    return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Idempotency key already used for another sale.");
                return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Refund idempotency key already used.");
            }

            _db.Payments.Add(new Payment
            {
                OrganizationId = orgId,
                BranchId = sale.BranchId,
                SaleId = sale.Id,
                Amount = -refundAmount,
                MethodCode = method,
                PaidAt = returnNow,
                CreatedAt = returnNow,
                ReceivedByUserId = _user.UserId!.Value,
                IdempotencyKey = key,
                Status = PaymentStatuses.Refunded
            });
            returnDoc.RefundedAt = returnNow;
            amountPaid -= refundAmount;
        }

        sale.AmountPaid = amountPaid;
        sale.BalanceDue = Math.Max(0, sale.TotalAmount - amountPaid);
        sale.UpdatedAt = DateTimeOffset.UtcNow;
        if (sale.Invoice is not null)
        {
            sale.Invoice.TotalAmount = sale.TotalAmount;
            sale.Invoice.AmountPaid = sale.AmountPaid;
            sale.Invoice.BalanceDue = sale.BalanceDue;
            sale.Invoice.Status = SaleWorkflow.InvoiceStatusFromBalances(sale.TotalAmount, sale.AmountPaid);
            sale.Invoice.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("return", "Sale", sale.Id.ToString(), null, new { returnDoc.ReturnNumber, returnDoc.RefundAmount }, ct);
        return Result<SaleDetailDto>.Success((await LoadDetailAsync(sale.Id, ct))!);
    }

    public async Task<Result<SaleDetailDto>> VoidAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesWrite);
        if (!auth.IsSuccess) return Result<SaleDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var sale = await _db.Sales
            .Include(s => s.Lines)
            .Include(s => s.Invoice)
            .Include(s => s.Returns)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (sale is null) return Result<SaleDetailDto>.Failure(ErrorCodes.NotFound, "Sale not found.");

        if (sale.Returns.Count > 0)
            return Result<SaleDetailDto>.Failure(ErrorCodes.Conflict, "Cannot void a sale that has returns.");

        var can = SaleWorkflow.CanVoid(sale.Status, sale.AmountPaid);
        if (!can.IsSuccess) return Result<SaleDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);

        foreach (var line in sale.Lines)
        {
            _db.StockLedgerEntries.Add(new StockLedgerEntry
            {
                OrganizationId = orgId,
                BranchId = sale.BranchId,
                PartId = line.PartId,
                QuantityDelta = line.Quantity,
                EntryType = StockEntryTypes.SaleReturn,
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                Reason = $"Void {sale.SaleNumber}",
                ActorUserId = _user.UserId!.Value
            });
        }

        sale.Status = SaleStatuses.Voided;
        sale.BalanceDue = 0;
        sale.VoidedAt = DateTimeOffset.UtcNow;
        sale.UpdatedAt = sale.VoidedAt;
        if (sale.Invoice is not null)
        {
            sale.Invoice.Status = InvoiceStatuses.Voided;
            sale.Invoice.BalanceDue = 0;
            sale.Invoice.VoidedAt = sale.VoidedAt;
            sale.Invoice.UpdatedAt = sale.VoidedAt;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("void", "Sale", sale.Id.ToString(), null, new { sale.Status }, ct);
        return Result<SaleDetailDto>.Success((await LoadDetailAsync(sale.Id, ct))!);
    }

    public async Task<Result<PagedResult<InvoiceDto>>> ListInvoicesAsync(PagedQuery query, bool unpaidOnly, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRead);
        if (!auth.IsSuccess) return Result<PagedResult<InvoiceDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Invoices.AsNoTracking().Where(i => i.OrganizationId == orgId);
        if (unpaidOnly)
            q = q.Where(i => i.BalanceDue > 0 && i.Status != InvoiceStatuses.Voided && i.Status != InvoiceStatuses.Paid);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(i => i.InvoiceNumber.ToLower().Contains(s));
        }

        q = q.OrderByDescending(i => i.IssuedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(i => new InvoiceDto(
                i.Id, i.SaleId, i.InvoiceNumber, i.Status, i.IssuedAt, i.DueAt, i.VoidedAt,
                i.CreatedAt, i.UpdatedAt, i.TotalAmount, i.AmountPaid, i.BalanceDue))
            .ToListAsync(ct);

        return Result<PagedResult<InvoiceDto>>.Success(new PagedResult<InvoiceDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<InvoiceDto>> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRead);
        if (!auth.IsSuccess) return Result<InvoiceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var inv = await _db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == _user.OrganizationId, ct);
        if (inv is null) return Result<InvoiceDto>.Failure(ErrorCodes.NotFound, "Invoice not found.");
        return Result<InvoiceDto>.Success(new InvoiceDto(
            inv.Id, inv.SaleId, inv.InvoiceNumber, inv.Status, inv.IssuedAt, inv.DueAt, inv.VoidedAt,
            inv.CreatedAt, inv.UpdatedAt, inv.TotalAmount, inv.AmountPaid, inv.BalanceDue));
    }

    public async Task<Result<IReadOnlyList<PaymentMethodDto>>> ListPaymentMethodsAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.SalesRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<PaymentMethodDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var items = await _db.PaymentMethods.AsNoTracking()
            .Where(m => m.OrganizationId == _user.OrganizationId && m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new PaymentMethodDto(m.Id, m.Code, m.Name))
            .ToListAsync(ct);
        return Result<IReadOnlyList<PaymentMethodDto>>.Success(items);
    }

    private async Task<string> NextNumberAsync(string prefix, Guid orgId, CancellationToken ct)
    {
        string? last = prefix switch
        {
            "SALE-" => await _db.Sales.AsNoTracking()
                .Where(r => r.OrganizationId == orgId && r.SaleNumber.StartsWith(prefix))
                .OrderByDescending(r => r.SaleNumber).Select(r => r.SaleNumber).FirstOrDefaultAsync(ct),
            "INV-" => await _db.Invoices.AsNoTracking()
                .Where(r => r.OrganizationId == orgId && r.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(r => r.InvoiceNumber).Select(r => r.InvoiceNumber).FirstOrDefaultAsync(ct),
            "RET-" => await _db.SaleReturns.AsNoTracking()
                .Where(r => r.OrganizationId == orgId && r.ReturnNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReturnNumber).Select(r => r.ReturnNumber).FirstOrDefaultAsync(ct),
            _ => null
        };
        var next = 1;
        if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var n))
            next = n + 1;
        return $"{prefix}{next:D5}";
    }

    private async Task<Dictionary<Guid, decimal>> OnHandMapAsync(Guid orgId, Guid branchId, List<Guid> partIds, CancellationToken ct)
    {
        if (partIds.Count == 0) return new Dictionary<Guid, decimal>();
        return await _db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.OrganizationId == orgId && e.BranchId == branchId && partIds.Contains(e.PartId))
            .GroupBy(e => e.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.QuantityDelta) })
            .ToDictionaryAsync(x => x.PartId, x => x.Qty, ct);
    }

    private async Task<SaleDetailDto?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var orgId = _user.OrganizationId!.Value;
        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.Invoice)
            .Include(s => s.Returns).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (sale is null) return null;

        string? customerName = null;
        if (sale.CustomerId.HasValue)
        {
            customerName = await _db.Customers.AsNoTracking()
                .Where(c => c.Id == sale.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        }

        return new SaleDetailDto(
            sale.Id,
            sale.SaleNumber,
            sale.BranchId,
            sale.CustomerId,
            customerName,
            sale.Status,
            sale.Subtotal,
            sale.DiscountTotal,
            sale.TaxTotal,
            sale.TotalAmount,
            sale.AmountPaid,
            sale.BalanceDue,
            sale.CompletedAt,
            sale.VoidedAt,
            sale.CreatedAt,
            sale.UpdatedAt,
            sale.Notes,
            sale.Lines.Select(l => new SaleLineDto(l.Id, l.PartId, l.Description, l.Quantity, l.UnitPrice, l.UnitCost, l.Discount, l.LineTotal)).ToList(),
            sale.Payments.OrderBy(p => p.PaidAt).Select(p => new PaymentDto(p.Id, p.Amount, p.MethodCode, p.PaidAt, p.CreatedAt, p.IdempotencyKey, p.Status)).ToList(),
            sale.Invoice is null ? null : new InvoiceDto(
                sale.Invoice.Id, sale.Invoice.SaleId, sale.Invoice.InvoiceNumber, sale.Invoice.Status,
                sale.Invoice.IssuedAt, sale.Invoice.DueAt, sale.Invoice.VoidedAt,
                sale.Invoice.CreatedAt, sale.Invoice.UpdatedAt,
                sale.Invoice.TotalAmount, sale.Invoice.AmountPaid, sale.Invoice.BalanceDue),
            sale.Returns.OrderByDescending(r => r.CompletedAt).Select(r => new SaleReturnDto(
                r.Id, r.ReturnNumber, r.CompletedAt, r.RefundedAt, r.RefundAmount, r.CreatedAt,
                r.Lines.Select(l => new SaleReturnLineDto(l.Id, l.SaleLineId, l.PartId, l.Quantity)).ToList())).ToList());
    }
}
