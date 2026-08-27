using Ersms.Application.Inventory;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parts")]
public sealed class PartsController : ControllerBase
{
    private readonly IPartService _parts;

    public PartsController(IPartService parts) => _parts = parts;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] Guid? branchId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _parts.ListAsync(query, branchId, includeInactive, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? branchId, CancellationToken ct = default)
    {
        var result = await _parts.GetAsync(id, branchId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> Ledger(Guid id, [FromQuery] PagedQuery query, [FromQuery] Guid? branchId, CancellationToken ct = default)
    {
        var result = await _parts.ListLedgerAsync(id, query, branchId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePartRequest request, CancellationToken ct)
    {
        var result = await _parts.CreateAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePartRequest request, CancellationToken ct)
    {
        var result = await _parts.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/adjustments")]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var result = await _parts.AdjustAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _parts.SetActiveAsync(id, false, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _parts.SetActiveAsync(id, true, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
