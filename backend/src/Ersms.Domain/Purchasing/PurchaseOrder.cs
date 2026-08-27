using Ersms.SharedKernel;

namespace Ersms.Domain.Purchasing;

public class Supplier : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class PurchaseOrderStatuses
{
    public const string Draft = "DRAFT";
    public const string Ordered = "ORDERED";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Received = "RECEIVED";
    public const string Cancelled = "CANCELLED";
}

public class PurchaseOrder : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string Status { get; set; } = PurchaseOrderStatuses.Draft;
    public DateTimeOffset? OrderedAt { get; set; }
    public string? Notes { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine : Entity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid PartId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
}

/// <summary>Stable receive-batch header for accounting source idempotency (one per Receive API call).</summary>
public class PurchaseReceive : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ReceivedByUserId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
}

public static class PurchaseOrderWorkflow
{
    public static Result CanSubmit(string status) =>
        status == PurchaseOrderStatuses.Draft
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Only draft purchase orders can be submitted.");

    public static Result CanCancel(string status) =>
        status is PurchaseOrderStatuses.Draft or PurchaseOrderStatuses.Ordered
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Only draft or ordered purchase orders can be cancelled.");

    public static Result CanReceive(string status) =>
        status is PurchaseOrderStatuses.Ordered or PurchaseOrderStatuses.PartiallyReceived
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Purchase order cannot receive stock in its current status.");

    public static string StatusAfterReceive(IEnumerable<PurchaseOrderLine> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return PurchaseOrderStatuses.Ordered;
        if (list.All(l => l.QuantityReceived >= l.QuantityOrdered))
            return PurchaseOrderStatuses.Received;
        if (list.Any(l => l.QuantityReceived > 0))
            return PurchaseOrderStatuses.PartiallyReceived;
        return PurchaseOrderStatuses.Ordered;
    }
}
