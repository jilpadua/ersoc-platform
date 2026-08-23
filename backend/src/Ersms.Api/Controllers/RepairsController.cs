using Ersms.Application.Repairs;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/repairs")]
public sealed class RepairsController : ControllerBase
{
    private readonly IRepairService _repairs;

    public RepairsController(IRepairService repairs) => _repairs = repairs;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] string? statusCode, CancellationToken ct)
    {
        var result = await _repairs.ListAsync(query, statusCode, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("statuses")]
    public async Task<IActionResult> Statuses(CancellationToken ct)
    {
        var result = await _repairs.ListStatusesAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _repairs.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRepairRequest request, CancellationToken ct)
    {
        var result = await _repairs.CreateAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeRepairStatusRequest request, CancellationToken ct)
    {
        var result = await _repairs.ChangeStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("{id:guid}/technician")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTechnicianRequest request, CancellationToken ct)
    {
        var result = await _repairs.AssignTechnicianAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddRepairNoteRequest request, CancellationToken ct)
    {
        var result = await _repairs.AddNoteAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
