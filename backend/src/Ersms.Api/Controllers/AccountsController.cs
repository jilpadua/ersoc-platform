using Ersms.Application.Accounting;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activeOnly, CancellationToken ct)
    {
        var result = await _accounts.ListAsync(activeOnly, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var result = await _accounts.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        var result = await _accounts.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("/api/v1/accounting/mappings")]
    public async Task<IActionResult> ListMappings(CancellationToken ct)
    {
        var result = await _accounts.ListMappingsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPut("/api/v1/accounting/mappings")]
    public async Task<IActionResult> UpsertMapping([FromBody] UpsertMappingRequest request, CancellationToken ct)
    {
        var result = await _accounts.UpsertMappingAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
