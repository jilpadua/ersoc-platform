using Ersms.Application.Accounting;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/supplier-bills")]
public sealed class SupplierBillsController : ControllerBase
{
    private readonly IApService _ap;

    public SupplierBillsController(IApService ap) => _ap = ap;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] PagedQuery query,
        [FromQuery] Guid? supplierId,
        [FromQuery] bool? unpaidOnly,
        CancellationToken ct)
    {
        var result = await _ap.ListBillsAsync(query, supplierId, unpaidOnly, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _ap.GetBillAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("/api/v1/supplier-payments")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordSupplierPaymentRequest request, CancellationToken ct)
    {
        var headerKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerKey) && string.IsNullOrWhiteSpace(request.IdempotencyKey))
            request = request with { IdempotencyKey = headerKey };

        var result = await _ap.RecordPaymentAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
