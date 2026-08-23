using Ersms.Application.Common;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Audit;

public sealed record AuditLogDto(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    string EntityId,
    string? BeforeJson,
    string? AfterJson,
    DateTimeOffset Timestamp);

public interface IAuditQueryService
{
    Task<Result<PagedResult<AuditLogDto>>> ListAsync(PagedQuery query, CancellationToken ct = default);
}

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public AuditQueryService(IApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<Result<PagedResult<AuditLogDto>>> ListAsync(PagedQuery query, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AuditRead);
        if (!auth.IsSuccess) return Result<PagedResult<AuditLogDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var q = _db.AuditLogs.AsNoTracking().Where(a => a.OrganizationId == _user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(a =>
                a.EntityType.ToLower().Contains(s) ||
                a.Action.ToLower().Contains(s) ||
                a.EntityId.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Timestamp)
            .Skip(query.Skip).Take(query.Take)
            .Select(a => new AuditLogDto(a.Id, a.ActorUserId, a.Action, a.EntityType, a.EntityId, a.BeforeJson, a.AfterJson, a.Timestamp))
            .ToListAsync(ct);

        return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
        {
            Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total
        });
    }
}
