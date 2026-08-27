using Ersms.Application.Accounting;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounting/periods")]
public sealed class AccountingPeriodsController : ControllerBase
{
    private readonly IAccountingPeriodService _periods;

    public AccountingPeriodsController(IAccountingPeriodService periods) => _periods = periods;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _periods.ListAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePeriodsRequest request, CancellationToken ct)
    {
        var result = await _periods.GenerateYearAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var result = await _periods.CloseAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken ct)
    {
        var result = await _periods.ReopenAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
