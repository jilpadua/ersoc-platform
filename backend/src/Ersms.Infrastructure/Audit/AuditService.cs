using System.Text.Json;
using Ersms.Application.Common;
using Ersms.Domain.Audit;
using Ersms.Infrastructure.Persistence;
using Ersms.SharedKernel;

namespace Ersms.Infrastructure.Audit;

public sealed class AuditService : IAuditService
{
    private readonly ErsmsDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditService(ErsmsDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? before,
        object? after,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.OrganizationId is null)
            return;

        _db.AuditLogs.Add(new AuditLog
        {
            OrganizationId = _currentUser.OrganizationId.Value,
            BranchId = _currentUser.BranchId,
            ActorUserId = _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = JsonAudit.Serialize(before),
            AfterJson = JsonAudit.Serialize(after),
            Timestamp = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
