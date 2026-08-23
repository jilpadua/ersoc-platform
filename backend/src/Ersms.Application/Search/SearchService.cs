using Ersms.Application.Common;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Search;

public sealed record SearchResultDto(string Type, Guid Id, string Title, string? Subtitle);

public interface ISearchService
{
    Task<Result<IReadOnlyList<SearchResultDto>>> SearchAsync(string query, CancellationToken ct = default);
}

public sealed class SearchService : ISearchService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public SearchService(IApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<SearchResultDto>>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!_user.IsAuthenticated || _user.OrganizationId is null)
            return Result<IReadOnlyList<SearchResultDto>>.Failure(ErrorCodes.Unauthorized, "Authentication required.");

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return Result<IReadOnlyList<SearchResultDto>>.Success(Array.Empty<SearchResultDto>());

        var orgId = _user.OrganizationId.Value;
        var s = query.Trim().ToLower();
        var results = new List<SearchResultDto>();

        var repairs = await _db.Repairs.AsNoTracking()
            .Where(r => r.OrganizationId == orgId && r.RepairNumber.ToLower().Contains(s))
            .OrderByDescending(r => r.ReceivedAt)
            .Take(10)
            .Select(r => new SearchResultDto("repair", r.Id, r.RepairNumber, r.ReportedProblem))
            .ToListAsync(ct);
        results.AddRange(repairs);

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.OrganizationId == orgId &&
                        (c.Name.ToLower().Contains(s) || (c.Phone != null && c.Phone.ToLower().Contains(s))))
            .OrderBy(c => c.Name)
            .Take(10)
            .Select(c => new SearchResultDto("customer", c.Id, c.Name, c.Phone))
            .ToListAsync(ct);
        results.AddRange(customers);

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.OrganizationId == orgId &&
                        (d.Model.ToLower().Contains(s) || d.Brand.ToLower().Contains(s) ||
                         (d.SerialNumber != null && d.SerialNumber.ToLower().Contains(s)) ||
                         (d.Imei != null && d.Imei.ToLower().Contains(s))))
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Select(d => new SearchResultDto("device", d.Id, d.Brand + " " + d.Model, d.SerialNumber ?? d.Imei))
            .ToListAsync(ct);
        results.AddRange(devices);

        return Result<IReadOnlyList<SearchResultDto>>.Success(results);
    }
}
