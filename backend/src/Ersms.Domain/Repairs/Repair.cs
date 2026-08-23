using Ersms.SharedKernel;

namespace Ersms.Domain.Repairs;

public class RepairStatusDefinition : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CountsAsPending { get; set; } = true;
}

public class Repair : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string RepairNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid StatusId { get; set; }
    public RepairStatusDefinition? Status { get; set; }
    public string ReportedProblem { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? Accessories { get; set; }
    public string? Diagnosis { get; set; }
    public Guid? TechnicianUserId { get; set; }
    public decimal? EstimateAmount { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public DateTimeOffset? ApprovedAt { get; set; }
    public int? WarrantyDays { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<RepairServiceLine> ServiceLines { get; set; } = new List<RepairServiceLine>();
    public ICollection<RepairStatusHistory> StatusHistory { get; set; } = new List<RepairStatusHistory>();
    public ICollection<RepairNote> Notes { get; set; } = new List<RepairNote>();
    public ICollection<RepairPhoto> Photos { get; set; } = new List<RepairPhoto>();

    public void RecalculateTotals()
    {
        Subtotal = ServiceLines.Sum(x => x.LineTotal);
        TotalAmount = Math.Max(0, Subtotal - DiscountTotal);
    }
}

public class RepairServiceLine : AuditableEntity
{
    public Guid RepairId { get; set; }
    public Repair? Repair { get; set; }
    public Guid? ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal => Math.Max(0, Quantity * UnitPrice - Discount);
}

public class RepairStatusHistory : Entity
{
    public Guid RepairId { get; set; }
    public Repair? Repair { get; set; }
    public Guid? PreviousStatusId { get; set; }
    public Guid NewStatusId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}

public class RepairNote : AuditableEntity
{
    public Guid RepairId { get; set; }
    public Repair? Repair { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class RepairPhoto : AuditableEntity
{
    public Guid RepairId { get; set; }
    public Repair? Repair { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
}

public static class DefaultRepairStatuses
{
    public static readonly (string Code, string Name, int Order, bool Terminal, bool Pending)[] All =
    [
        ("RECEIVED", "Received", 10, false, true),
        ("DIAGNOSIS", "Diagnosis", 20, false, true),
        ("WAITING_FOR_APPROVAL", "Waiting for Approval", 30, false, true),
        ("APPROVED", "Approved", 40, false, true),
        ("WAITING_FOR_PARTS", "Waiting for Parts", 50, false, true),
        ("REPAIRING", "Repairing", 60, false, true),
        ("TESTING", "Testing", 70, false, true),
        ("READY_FOR_PICKUP", "Ready for Pickup", 80, false, true),
        ("COMPLETED", "Completed", 90, true, false),
        ("CANCELLED", "Cancelled", 100, true, false)
    ];
}

public static class RepairWorkflow
{
    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RECEIVED"] = ["DIAGNOSIS", "CANCELLED"],
        ["DIAGNOSIS"] = ["WAITING_FOR_APPROVAL", "APPROVED", "CANCELLED"],
        ["WAITING_FOR_APPROVAL"] = ["APPROVED", "CANCELLED"],
        ["APPROVED"] = ["WAITING_FOR_PARTS", "REPAIRING", "CANCELLED"],
        ["WAITING_FOR_PARTS"] = ["REPAIRING", "CANCELLED"],
        ["REPAIRING"] = ["TESTING", "WAITING_FOR_PARTS", "CANCELLED"],
        ["TESTING"] = ["READY_FOR_PICKUP", "REPAIRING", "CANCELLED"],
        ["READY_FOR_PICKUP"] = ["COMPLETED", "CANCELLED"],
        ["COMPLETED"] = [],
        ["CANCELLED"] = []
    };

    public static Result CanTransition(string fromCode, string toCode)
    {
        if (string.Equals(fromCode, toCode, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(ErrorCodes.InvalidTransition, "Status is already set to that value.");

        if (!Allowed.TryGetValue(fromCode, out var next) || !next.Contains(toCode))
            return Result.Failure(ErrorCodes.InvalidTransition, $"Cannot transition from {fromCode} to {toCode}.");

        return Result.Success();
    }
}

public sealed class RepairCreatedEvent : DomainEventBase
{
    public required Guid RepairId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string RepairNumber { get; init; }
}

public sealed class RepairStatusChangedEvent : DomainEventBase
{
    public required Guid RepairId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string PreviousStatusCode { get; init; }
    public required string NewStatusCode { get; init; }
    public required Guid ActorUserId { get; init; }
}
