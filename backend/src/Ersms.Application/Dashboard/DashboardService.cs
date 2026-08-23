using Ersms.Application.Common;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Dashboard;

public sealed record DashboardDto(
    decimal TodayCompletedRepairRevenue,
    int PendingRepairs,
    int OverdueRepairs,
    int CompletedToday,
    IReadOnlyList<TechnicianWorkloadDto> TechnicianWorkload,
    DashboardUnavailableDto Unavailable);

public sealed record TechnicianWorkloadDto(Guid? TechnicianUserId, int OpenRepairs);
public sealed record DashboardUnavailableDto(string Sales, string LowStock, string Expenses, string CashBalance, string UnpaidInvoices);

public interface IDashboardService
{
    Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public DashboardService(IApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.DashboardRead);
        if (!auth.IsSuccess) return Result<DashboardDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var todayStart = DateTimeOffset.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var completedStatusIds = await _db.RepairStatusDefinitions.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.Code == "COMPLETED")
            .Select(s => s.Id)
            .ToListAsync(ct);

        var pendingStatusIds = await _db.RepairStatusDefinitions.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.CountsAsPending)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var repairs = _db.Repairs.AsNoTracking().Where(r => r.OrganizationId == orgId);

        var todayRevenue = await repairs
            .Where(r => r.CompletedAt >= todayStart && r.CompletedAt < todayEnd && completedStatusIds.Contains(r.StatusId))
            .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;

        var pending = await repairs.CountAsync(r => pendingStatusIds.Contains(r.StatusId), ct);
        var overdue = await repairs.CountAsync(r =>
            pendingStatusIds.Contains(r.StatusId) && r.DueAt != null && r.DueAt < DateTimeOffset.UtcNow, ct);
        var completedToday = await repairs.CountAsync(r =>
            r.CompletedAt >= todayStart && r.CompletedAt < todayEnd && completedStatusIds.Contains(r.StatusId), ct);

        var workload = await repairs
            .Where(r => pendingStatusIds.Contains(r.StatusId))
            .GroupBy(r => r.TechnicianUserId)
            .Select(g => new TechnicianWorkloadDto(g.Key, g.Count()))
            .OrderByDescending(x => x.OpenRepairs)
            .ToListAsync(ct);

        return Result<DashboardDto>.Success(new DashboardDto(
            todayRevenue,
            pending,
            overdue,
            completedToday,
            workload,
            new DashboardUnavailableDto(
                "Available in Phase 3",
                "Available in Phase 2",
                "Available in Phase 4",
                "Available in Phase 4",
                "Available in Phase 3")));
    }
}
