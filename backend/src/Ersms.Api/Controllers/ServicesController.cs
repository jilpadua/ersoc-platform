using Ersms.Application.ServiceCatalog;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _services;

    public ServicesController(IServiceCatalogService services) => _services = services;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, CancellationToken ct)
    {
        var result = await _services.ListAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var result = await _services.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var result = await _services.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var result = await _services.ListCategoriesAsync(ct);
        if (!result.IsSuccess) return ApiErrors.FromResult(result);
        return Ok(result.Value!.Select(c => new { id = c.Id, name = c.Name }));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryBody body, CancellationToken ct)
    {
        var result = await _services.CreateCategoryAsync(body.Name, ct);
        if (!result.IsSuccess) return ApiErrors.FromResult(result);
        return Ok(new { id = result.Value.Id, name = result.Value.Name });
    }

    public sealed record CreateCategoryBody(string Name);
}
