using Ersms.Application.Accounting;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/journals")]
public sealed class JournalsController : ControllerBase
{
    private readonly IJournalQueryService _journals;

    public JournalsController(IJournalQueryService journals) => _journals = journals;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] string? sourceType, CancellationToken ct)
    {
        var result = await _journals.ListAsync(query, sourceType, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _journals.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("by-source")]
    public async Task<IActionResult> GetBySource([FromQuery] string sourceType, [FromQuery] Guid sourceId, CancellationToken ct)
    {
        var result = await _journals.GetBySourceAsync(sourceType, sourceId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("manual")]
    public async Task<IActionResult> PostManual([FromBody] ManualJournalRequest request, CancellationToken ct)
    {
        var result = await _journals.PostManualAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("opening-balances")]
    public async Task<IActionResult> PostOpeningBalances([FromBody] OpeningBalanceRequest request, CancellationToken ct)
    {
        var result = await _journals.PostOpeningBalancesAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
