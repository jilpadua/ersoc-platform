using Ersms.Application.Accounting;
using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.Domain.Inventory;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Inventory;

public sealed record PartDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal UnitCost,
    decimal UnitPrice,
    decimal ReorderLevel,
    decimal QuantityOnHand,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreatePartRequest(
    string Sku,
    string Name,
    string? Description,
    decimal UnitCost,
    decimal UnitPrice,
    decimal ReorderLevel,
    bool? IsActive = null);

public sealed record AdjustStockRequest(decimal QuantityDelta, string? Reason, Guid? BranchId = null);

public sealed record StockLedgerEntryDto(
    Guid Id,
    Guid BranchId,
    decimal QuantityDelta,
    string EntryType,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Reason,
    Guid ActorUserId,
    DateTimeOffset CreatedAt);

public sealed class CreatePartValidator : AbstractValidator<CreatePartRequest>
{
    public CreatePartValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.QuantityDelta).NotEqual(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public interface IPartService
{
    Task<Result<PagedResult<PartDto>>> ListAsync(PagedQuery query, Guid? branchId, bool includeInactive = false, CancellationToken ct = default);
    Task<Result<PartDto>> GetAsync(Guid id, Guid? branchId, CancellationToken ct = default);
    Task<Result<PartDto>> CreateAsync(CreatePartRequest request, CancellationToken ct = default);
    Task<Result<PartDto>> UpdateAsync(Guid id, CreatePartRequest request, CancellationToken ct = default);
    Task<Result<PartDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result<PartDto>> AdjustAsync(Guid id, AdjustStockRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<StockLedgerEntryDto>>> ListLedgerAsync(Guid id, PagedQuery query, Guid? branchId, CancellationToken ct = default);
}

public sealed class PartService : IPartService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IAccountingPostingService _posting;
    private readonly IValidator<CreatePartRequest> _validator;
    private readonly IValidator<AdjustStockRequest> _adjustValidator;

    public PartService(
        IApplicationDbContext db,
        ICurrentUser user,
        IAuditService audit,
        IAccountingPostingService posting,
        IValidator<CreatePartRequest> validator,
        IValidator<AdjustStockRequest> adjustValidator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _posting = posting;
        _validator = validator;
        _adjustValidator = adjustValidator;
    }

    public async Task<Result<PagedResult<PartDto>>> ListAsync(PagedQuery query, Guid? branchId, bool includeInactive = false, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryRead);
        if (!auth.IsSuccess) return Result<PagedResult<PartDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var branch = ResolveBranchId(branchId);
        if (branch is null)
            return Result<PagedResult<PartDto>>.Failure(ErrorCodes.Validation, "Branch is required for stock quantities.");

        var q = _db.Parts.AsNoTracking().Where(p => p.OrganizationId == orgId);
        if (!includeInactive) q = q.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(p => p.Sku.ToLower().Contains(s) || p.Name.ToLower().Contains(s));
        }

        q = query.SortDesc ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name);

        var total = await q.CountAsync(ct);
        var parts = await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        var ids = parts.Select(p => p.Id).ToList();
        var onHand = await OnHandMapAsync(orgId, branch.Value, ids, ct);
        var items = parts.Select(p => ToDto(p, onHand.GetValueOrDefault(p.Id))).ToList();

        return Result<PagedResult<PartDto>>.Success(new PagedResult<PartDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<PartDto>> GetAsync(Guid id, Guid? branchId, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryRead);
        if (!auth.IsSuccess) return Result<PartDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var branch = ResolveBranchId(branchId);
        if (branch is null)
            return Result<PartDto>.Failure(ErrorCodes.Validation, "Branch is required for stock quantities.");

        var part = await _db.Parts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (part is null) return Result<PartDto>.Failure(ErrorCodes.NotFound, "Part not found.");

        var qty = await OnHandAsync(orgId, branch.Value, id, ct);
        return Result<PartDto>.Success(ToDto(part, qty));
    }

    public async Task<Result<PartDto>> CreateAsync(CreatePartRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryWrite);
        if (!auth.IsSuccess) return Result<PartDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PartDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var sku = request.Sku.Trim();
        var exists = await _db.Parts.AnyAsync(p => p.OrganizationId == orgId && p.Sku == sku, ct);
        if (exists) return Result<PartDto>.Failure(ErrorCodes.Conflict, "SKU already exists.");

        var entity = new Part
        {
            OrganizationId = orgId,
            Sku = sku,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            UnitCost = request.UnitCost,
            UnitPrice = request.UnitPrice,
            ReorderLevel = request.ReorderLevel,
            IsActive = request.IsActive ?? true
        };
        _db.Parts.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Part", entity.Id.ToString(), null, ToDto(entity, 0), ct);
        return Result<PartDto>.Success(ToDto(entity, 0));
    }

    public async Task<Result<PartDto>> UpdateAsync(Guid id, CreatePartRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryWrite);
        if (!auth.IsSuccess) return Result<PartDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PartDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (entity is null) return Result<PartDto>.Failure(ErrorCodes.NotFound, "Part not found.");

        var sku = request.Sku.Trim();
        var skuTaken = await _db.Parts.AnyAsync(p => p.OrganizationId == orgId && p.Sku == sku && p.Id != id, ct);
        if (skuTaken) return Result<PartDto>.Failure(ErrorCodes.Conflict, "SKU already exists.");

        var branch = ResolveBranchId(null);
        var qty = branch is null ? 0m : await OnHandAsync(orgId, branch.Value, id, ct);
        var before = ToDto(entity, qty);

        entity.Sku = sku;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.UnitCost = request.UnitCost;
        entity.UnitPrice = request.UnitPrice;
        entity.ReorderLevel = request.ReorderLevel;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Part", entity.Id.ToString(), before, ToDto(entity, qty), ct);
        return Result<PartDto>.Success(ToDto(entity, qty));
    }

    public async Task<Result<PartDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryWrite);
        if (!auth.IsSuccess) return Result<PartDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (entity is null) return Result<PartDto>.Failure(ErrorCodes.NotFound, "Part not found.");

        var branch = ResolveBranchId(null);
        var qty = branch is null ? 0m : await OnHandAsync(orgId, branch.Value, id, ct);
        var before = ToDto(entity, qty);
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(isActive ? "activate" : "deactivate", "Part", entity.Id.ToString(), before, ToDto(entity, qty), ct);
        return Result<PartDto>.Success(ToDto(entity, qty));
    }

    public async Task<Result<PartDto>> AdjustAsync(Guid id, AdjustStockRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryWrite);
        if (!auth.IsSuccess) return Result<PartDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _adjustValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PartDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var branch = ResolveBranchId(request.BranchId);
        if (branch is null)
            return Result<PartDto>.Failure(ErrorCodes.Validation, "Branch is required for stock adjustments.");

        var entity = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (entity is null) return Result<PartDto>.Failure(ErrorCodes.NotFound, "Part not found.");

        var onHand = await OnHandAsync(orgId, branch.Value, id, ct);
        var check = StockMath.ApplyAdjustment(onHand, request.QuantityDelta);
        if (!check.IsSuccess)
            return Result<PartDto>.Failure(check.ErrorCode!, check.ErrorMessage!);

        var ledger = new StockLedgerEntry
        {
            OrganizationId = orgId,
            BranchId = branch.Value,
            PartId = id,
            QuantityDelta = request.QuantityDelta,
            EntryType = StockEntryTypes.Adjustment,
            Reason = request.Reason?.Trim(),
            ActorUserId = _user.UserId!.Value
        };
        _db.StockLedgerEntries.Add(ledger);

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            await _db.SaveChangesAsync(ct);

            var valueDelta = Math.Round(entity.UnitCost * request.QuantityDelta, 2);
            if (valueDelta != 0)
            {
                var maps = await AccountingLineBuilders.LoadMapsAsync(_db, orgId, ct);
                var lines = AccountingLineBuilders.StockAdjusted(maps, valueDelta);
                if (!lines.IsSuccess)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PartDto>.Failure(lines.ErrorCode!, lines.ErrorMessage!);
                }
                if (lines.Value!.Count > 0)
                {
                    var journal = await _posting.PostAsync(new PostJournalRequest(
                        orgId, branch, ledger.CreatedAt,
                        AccountingSourceTypes.StockAdjusted, ledger.Id,
                        $"Stock adjust {entity.Sku}", lines.Value), ct);
                    if (!journal.IsSuccess)
                    {
                        await tx.RollbackAsync(ct);
                        return Result<PartDto>.Failure(journal.ErrorCode!, journal.ErrorMessage!);
                    }
                }
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await _audit.WriteAsync("adjust", "Part", id.ToString(), new { onHand }, new { quantityOnHand = check.Value, request.QuantityDelta }, ct);
        return Result<PartDto>.Success(ToDto(entity, check.Value));
    }

    public async Task<Result<PagedResult<StockLedgerEntryDto>>> ListLedgerAsync(Guid id, PagedQuery query, Guid? branchId, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.InventoryRead);
        if (!auth.IsSuccess) return Result<PagedResult<StockLedgerEntryDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var exists = await _db.Parts.AnyAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (!exists) return Result<PagedResult<StockLedgerEntryDto>>.Failure(ErrorCodes.NotFound, "Part not found.");

        var q = _db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.OrganizationId == orgId && e.PartId == id);
        if (branchId.HasValue || _user.BranchId.HasValue)
        {
            var branch = ResolveBranchId(branchId);
            if (branch.HasValue) q = q.Where(e => e.BranchId == branch.Value);
        }

        q = q.OrderByDescending(e => e.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(e => new StockLedgerEntryDto(e.Id, e.BranchId, e.QuantityDelta, e.EntryType, e.ReferenceType, e.ReferenceId, e.Reason, e.ActorUserId, e.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<StockLedgerEntryDto>>.Success(new PagedResult<StockLedgerEntryDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    private Guid? ResolveBranchId(Guid? branchId) => branchId ?? _user.BranchId;

    private async Task<decimal> OnHandAsync(Guid orgId, Guid branchId, Guid partId, CancellationToken ct) =>
        await _db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.OrganizationId == orgId && e.BranchId == branchId && e.PartId == partId)
            .SumAsync(e => (decimal?)e.QuantityDelta, ct) ?? 0m;

    private async Task<Dictionary<Guid, decimal>> OnHandMapAsync(Guid orgId, Guid branchId, List<Guid> partIds, CancellationToken ct)
    {
        if (partIds.Count == 0) return new Dictionary<Guid, decimal>();
        return await _db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.OrganizationId == orgId && e.BranchId == branchId && partIds.Contains(e.PartId))
            .GroupBy(e => e.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.QuantityDelta) })
            .ToDictionaryAsync(x => x.PartId, x => x.Qty, ct);
    }

    private static PartDto ToDto(Part p, decimal qty) =>
        new(p.Id, p.Sku, p.Name, p.Description, p.UnitCost, p.UnitPrice, p.ReorderLevel, qty, p.IsActive, p.CreatedAt);
}
