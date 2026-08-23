using Ersms.Application.Devices;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class DevicesController : ControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices) => _devices = devices;

    [HttpGet("devices")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] Guid? customerId, CancellationToken ct)
    {
        var result = await _devices.ListAsync(query, customerId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("customers/{customerId:guid}/devices")]
    public async Task<IActionResult> ListForCustomer(Guid customerId, [FromQuery] PagedQuery query, CancellationToken ct)
    {
        var result = await _devices.ListAsync(query, customerId, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("devices/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _devices.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("devices")]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest request, CancellationToken ct)
    {
        var result = await _devices.CreateAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("devices/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateDeviceRequest request, CancellationToken ct)
    {
        var result = await _devices.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
