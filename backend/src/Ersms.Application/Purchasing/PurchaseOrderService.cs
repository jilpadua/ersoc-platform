using Ersms.Application.Common;
using Ersms.Domain.Inventory;
using Ersms.Domain.Purchasing;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Purchasing;

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid PartId,
    string? PartSku,
    string? PartName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal UnitCost);

public sealed record PurchaseOrderListDto(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string? SupplierName,
    Guid BranchId,
    string Status,
    DateTimeOffset? OrderedAt,
    DateTimeOffset CreatedAt);

public sealed record PurchaseOrderDetailDto(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string? SupplierName,
    Guid BranchId,
    string Status,
    DateTimeOffset? OrderedAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderLineInput(Guid PartId, decimal QuantityOrdered, decimal UnitCost);

public sealed record CreatePurchaseOrderRequest(Guid SupplierId, Guid? BranchId, string? Notes, IReadOnlyList<PurchaseOrderLineInput> Lines);

public sealed record UpdatePurchaseOrderRequest(Guid SupplierId, string? Notes, IReadOnlyList<PurchaseOrderLineInput> Lines);

public sealed record ReceiveLineInput(Guid LineId, decimal Quantity);

public sealed record ReceivePurchaseOrderRequest(IReadOnlyList<ReceiveLineInput> Lines);

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PartId).NotEmpty();
            line.RuleFor(l => l.QuantityOrdered).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public interface IPurchaseOrderService
{
    Task<Result<PagedResult<PurchaseOrderListDto>>> ListAsync(PagedQuery query, string? status, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> UpdateDraftAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> CancelAsync(Guid id, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> ReceiveAsync(Guid id, ReceivePurchaseOrderRequest request, CancellationToken ct = default);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreatePurchaseOrderRequest> _validator;

    public PurchaseOrderService(
        IApplicationDbContext db,
        ICurrentUser user,
        IAuditService audit,
        IValidator<CreatePurchaseOrderRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<PurchaseOrderListDto>>> ListAsync(PagedQuery query, string? status, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingRead);
        if (!auth.IsSuccess) return Result<PagedResult<PurchaseOrderListDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = from po in _db.PurchaseOrders.AsNoTracking()
                join s in _db.Suppliers.AsNoTracking() on po.SupplierId equals s.Id
                where po.OrganizationId == orgId
                select new { po, SupplierName = s.Name };

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.po.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x => x.po.PoNumber.ToLower().Contains(s) || x.SupplierName.ToLower().Contains(s));
        }

        q = q.OrderByDescending(x => x.po.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(x => new PurchaseOrderListDto(
                x.po.Id, x.po.PoNumber, x.po.SupplierId, x.SupplierName, x.po.BranchId,
                x.po.Status, x.po.OrderedAt, x.po.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<PurchaseOrderListDto>>.Success(new PagedResult<PurchaseOrderListDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<PurchaseOrderDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingRead);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var detail = await LoadDetailAsync(id, ct);
        if (detail is null) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Purchase order not found.");
        return Result<PurchaseOrderDetailDto>.Success(detail);
    }

    public async Task<Result<PurchaseOrderDetailDto>> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var branchId = request.BranchId ?? _user.BranchId;
        if (branchId is null)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "Branch is required.");

        var supplierOk = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.OrganizationId == orgId && s.IsActive, ct);
        if (!supplierOk) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");

        var partIds = request.Lines.Select(l => l.PartId).Distinct().ToList();
        var partsOk = await _db.Parts.CountAsync(p => p.OrganizationId == orgId && p.IsActive && partIds.Contains(p.Id), ct);
        if (partsOk != partIds.Count)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "One or more parts are invalid.");

        var po = new PurchaseOrder
        {
            OrganizationId = orgId,
            BranchId = branchId.Value,
            SupplierId = request.SupplierId,
            PoNumber = await NextPoNumberAsync(orgId, ct),
            Status = PurchaseOrderStatuses.Draft,
            Notes = request.Notes
        };
        foreach (var line in request.Lines)
        {
            po.Lines.Add(new PurchaseOrderLine
            {
                PartId = line.PartId,
                QuantityOrdered = line.QuantityOrdered,
                QuantityReceived = 0,
                UnitCost = line.UnitCost
            });
        }

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "PurchaseOrder", po.Id.ToString(), null, new { po.PoNumber, po.Status }, ct);
        return Result<PurchaseOrderDetailDto>.Success((await LoadDetailAsync(po.Id, ct))!);
    }

    public async Task<Result<PurchaseOrderDetailDto>> UpdateDraftAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var po = await _db.PurchaseOrders.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (po is null) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Purchase order not found.");
        if (po.Status != PurchaseOrderStatuses.Draft)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.InvalidTransition, "Only draft purchase orders can be edited.");

        if (request.Lines.Count == 0)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "At least one line is required.");

        var supplierOk = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.OrganizationId == orgId && s.IsActive, ct);
        if (!supplierOk) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");

        var partIds = request.Lines.Select(l => l.PartId).Distinct().ToList();
        var partsOk = await _db.Parts.CountAsync(p => p.OrganizationId == orgId && p.IsActive && partIds.Contains(p.Id), ct);
        if (partsOk != partIds.Count)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "One or more parts are invalid.");

        po.SupplierId = request.SupplierId;
        po.Notes = request.Notes;
        _db.PurchaseOrderLines.RemoveRange(po.Lines);
        po.Lines.Clear();
        foreach (var line in request.Lines)
        {
            po.Lines.Add(new PurchaseOrderLine
            {
                PartId = line.PartId,
                QuantityOrdered = line.QuantityOrdered,
                QuantityReceived = 0,
                UnitCost = line.UnitCost
            });
        }
        po.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "PurchaseOrder", po.Id.ToString(), null, new { po.PoNumber, po.Status }, ct);
        return Result<PurchaseOrderDetailDto>.Success((await LoadDetailAsync(po.Id, ct))!);
    }

    public async Task<Result<PurchaseOrderDetailDto>> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var po = await _db.PurchaseOrders.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (po is null) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Purchase order not found.");

        var can = PurchaseOrderWorkflow.CanSubmit(po.Status);
        if (!can.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);
        if (po.Lines.Count == 0)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "Cannot submit a purchase order with no lines.");

        po.Status = PurchaseOrderStatuses.Ordered;
        po.OrderedAt = DateTimeOffset.UtcNow;
        po.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("submit", "PurchaseOrder", po.Id.ToString(), null, new { po.Status }, ct);
        return Result<PurchaseOrderDetailDto>.Success((await LoadDetailAsync(po.Id, ct))!);
    }

    public async Task<Result<PurchaseOrderDetailDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (po is null) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Purchase order not found.");

        var can = PurchaseOrderWorkflow.CanCancel(po.Status);
        if (!can.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);

        po.Status = PurchaseOrderStatuses.Cancelled;
        po.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cancel", "PurchaseOrder", po.Id.ToString(), null, new { po.Status }, ct);
        return Result<PurchaseOrderDetailDto>.Success((await LoadDetailAsync(po.Id, ct))!);
    }

    public async Task<Result<PurchaseOrderDetailDto>> ReceiveAsync(Guid id, ReceivePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        if (request.Lines.Count == 0)
            return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "At least one receive line is required.");

        var orgId = _user.OrganizationId!.Value;
        var po = await _db.PurchaseOrders.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (po is null) return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.NotFound, "Purchase order not found.");

        var can = PurchaseOrderWorkflow.CanReceive(po.Status);
        if (!can.IsSuccess) return Result<PurchaseOrderDetailDto>.Failure(can.ErrorCode!, can.ErrorMessage!);

        var receive = new PurchaseReceive
        {
            OrganizationId = orgId,
            PurchaseOrderId = po.Id,
            ReceivedAt = DateTimeOffset.UtcNow,
            ReceivedByUserId = _user.UserId!.Value
        };
        _db.PurchaseReceives.Add(receive);

        foreach (var recv in request.Lines)
        {
            if (recv.Quantity <= 0)
                return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "Receive quantity must be positive.");

            var line = po.Lines.FirstOrDefault(l => l.Id == recv.LineId);
            if (line is null)
                return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "Invalid purchase order line.");

            var remaining = line.QuantityOrdered - line.QuantityReceived;
            if (recv.Quantity > remaining)
                return Result<PurchaseOrderDetailDto>.Failure(ErrorCodes.Validation, "Cannot receive more than remaining ordered quantity.");

            line.QuantityReceived += recv.Quantity;
            _db.StockLedgerEntries.Add(new StockLedgerEntry
            {
                OrganizationId = orgId,
                BranchId = po.BranchId,
                PartId = line.PartId,
                QuantityDelta = recv.Quantity,
                EntryType = StockEntryTypes.PurchaseReceive,
                ReferenceType = "PurchaseReceive",
                ReferenceId = receive.Id,
                Reason = $"Received on {po.PoNumber}",
                ActorUserId = _user.UserId!.Value
            });
        }

        po.Status = PurchaseOrderWorkflow.StatusAfterReceive(po.Lines);
        po.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("receive", "PurchaseOrder", po.Id.ToString(), null, new { po.Status, ReceiveId = receive.Id }, ct);
        return Result<PurchaseOrderDetailDto>.Success((await LoadDetailAsync(po.Id, ct))!);
    }

    private async Task<string> NextPoNumberAsync(Guid orgId, CancellationToken ct)
    {
        const string prefix = "PO-";
        var last = await _db.PurchaseOrders.AsNoTracking()
            .Where(r => r.OrganizationId == orgId && r.PoNumber.StartsWith(prefix))
            .OrderByDescending(r => r.PoNumber)
            .Select(r => r.PoNumber)
            .FirstOrDefaultAsync(ct);
        var next = 1;
        if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var n))
            next = n + 1;
        return $"{prefix}{next:D5}";
    }

    private async Task<PurchaseOrderDetailDto?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var orgId = _user.OrganizationId!.Value;
        var po = await _db.PurchaseOrders.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (po is null) return null;

        var supplierName = await _db.Suppliers.AsNoTracking()
            .Where(s => s.Id == po.SupplierId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        var partIds = po.Lines.Select(l => l.PartId).Distinct().ToList();
        var parts = await _db.Parts.AsNoTracking()
            .Where(p => partIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToDictionaryAsync(p => p.Id, ct);

        return new PurchaseOrderDetailDto(
            po.Id,
            po.PoNumber,
            po.SupplierId,
            supplierName,
            po.BranchId,
            po.Status,
            po.OrderedAt,
            po.Notes,
            po.CreatedAt,
            po.Lines.Select(l =>
            {
                parts.TryGetValue(l.PartId, out var part);
                return new PurchaseOrderLineDto(l.Id, l.PartId, part?.Sku, part?.Name, l.QuantityOrdered, l.QuantityReceived, l.UnitCost);
            }).ToList());
    }
}
