using Ersms.SharedKernel;

namespace Ersms.Domain.Inventory;

public class Part : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class StockEntryTypes
{
    public const string Adjustment = "Adjustment";
    public const string PurchaseReceive = "PurchaseReceive";
    public const string Sale = "Sale";
    public const string SaleReturn = "SaleReturn";
}

public class StockLedgerEntry : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PartId { get; set; }
    public decimal QuantityDelta { get; set; }
    public string EntryType { get; set; } = StockEntryTypes.Adjustment;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Reason { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Part? Part { get; set; }
}

public static class StockMath
{
    public static Result<decimal> ApplyAdjustment(decimal onHand, decimal delta)
    {
        var next = onHand + delta;
        if (next < 0)
            return Result<decimal>.Failure(ErrorCodes.Conflict, "Adjustment would result in negative stock.");
        return Result<decimal>.Success(next);
    }
}
