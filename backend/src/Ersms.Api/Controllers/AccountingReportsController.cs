using Ersms.Application.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounting/reports")]
public sealed class AccountingReportsController : ControllerBase
{
    private readonly IAccountingReportService _reports;

    public AccountingReportsController(IAccountingReportService reports) => _reports = reports;

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GeneralLedger(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? accountId,
        CancellationToken ct)
    {
        var result = await _reports.GeneralLedgerAsync(from, to, accountId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] DateTimeOffset asOf, CancellationToken ct)
    {
        var result = await _reports.TrialBalanceAsync(asOf, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> ProfitAndLoss(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var result = await _reports.ProfitAndLossAsync(from, to, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateTimeOffset asOf, CancellationToken ct)
    {
        var result = await _reports.BalanceSheetAsync(asOf, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var result = await _reports.CashFlowAsync(from, to, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("ar-aging")]
    public async Task<IActionResult> ArAging([FromQuery] DateTimeOffset asOf, CancellationToken ct)
    {
        var result = await _reports.ArAgingAsync(asOf, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("ap-aging")]
    public async Task<IActionResult> ApAging([FromQuery] DateTimeOffset asOf, CancellationToken ct)
    {
        var result = await _reports.ApAgingAsync(asOf, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("customer-statement")]
    public async Task<IActionResult> CustomerStatement(
        [FromQuery] Guid customerId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var result = await _reports.CustomerStatementAsync(customerId, from, to, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("reconciliation")]
    public async Task<IActionResult> RunReconciliation([FromQuery] DateTimeOffset asOf, CancellationToken ct)
    {
        var result = await _reports.RunReconciliationAsync(asOf, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
