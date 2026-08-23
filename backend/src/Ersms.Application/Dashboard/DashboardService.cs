using Ersms.Application.Common;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Dashboard;

public sealed record DashboardDto(
    decimal TodayCompletedRepairRevenue,
    int PendingRepairs,
    int OverdueRepairs,
    int CompletedToday,
    int LowStockParts,
    IReadOnlyList<TechnicianWorkloadDto> TechnicianWorkload,
    DashboardUnavailableDto Unavailable);

public sealed record TechnicianWorkloadDto(Guid? TechnicianUserId, int OpenRepairs);
public sealed record DashboardUnavailableDto(string Sales, string Expenses, string CashBalance, string UnpaidInvoices);

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
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
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

        var workloadRows = await repairs
            .Where(r => pendingStatusIds.Contains(r.StatusId))
            .GroupBy(r => r.TechnicianUserId)
            .Select(g => new { TechnicianUserId = g.Key, OpenRepairs = g.Count() })
            .OrderByDescending(x => x.OpenRepairs)
            .ToListAsync(ct);

        var workload = workloadRows
            .Select(x => new TechnicianWorkloadDto(x.TechnicianUserId, x.OpenRepairs))
            .ToList();

        var lowStock = 0;
        var branchId = _user.BranchId;
        if (branchId.HasValue)
        {
            var parts = await _db.Parts.AsNoTracking()
                .Where(p => p.OrganizationId == orgId && p.IsActive)
                .Select(p => new { p.Id, p.ReorderLevel })
                .ToListAsync(ct);
            var onHand = await _db.StockLedgerEntries.AsNoTracking()
                .Where(e => e.OrganizationId == orgId && e.BranchId == branchId.Value)
                .GroupBy(e => e.PartId)
                .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.QuantityDelta) })
                .ToDictionaryAsync(x => x.PartId, x => x.Qty, ct);

            lowStock = parts.Count(p => onHand.GetValueOrDefault(p.Id) < p.ReorderLevel);
        }

        return Result<DashboardDto>.Success(new DashboardDto(
            todayRevenue,
            pending,
            overdue,
            completedToday,
            lowStock,
            workload,
            new DashboardUnavailableDto(
                "Available in Phase 3",
                "Available in Phase 4",
                "Available in Phase 4",
                "Available in Phase 3")));
    }
}
