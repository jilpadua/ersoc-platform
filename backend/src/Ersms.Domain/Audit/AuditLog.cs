using Ersms.SharedKernel;

namespace Ersms.Domain.Audit;

public class AuditLog : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
