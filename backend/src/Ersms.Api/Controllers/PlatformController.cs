using Ersms.Application.Audit;
using Ersms.Application.Dashboard;
using Ersms.Application.Search;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class PlatformController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ISearchService _search;
    private readonly IAuditQueryService _audit;

    public PlatformController(IDashboardService dashboard, ISearchService search, IAuditQueryService audit)
    {
        _dashboard = dashboard;
        _search = search;
        _audit = audit;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var result = await _dashboard.GetAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _search.SearchAsync(q, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] PagedQuery query, CancellationToken ct)
    {
        var result = await _audit.ListAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
