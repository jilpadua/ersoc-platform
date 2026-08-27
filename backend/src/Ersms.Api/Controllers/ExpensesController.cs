using Ersms.Application.Accounting;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenses;

    public ExpensesController(IExpenseService expenses) => _expenses = expenses;

    [HttpGet("categories")]
    public async Task<IActionResult> ListCategories([FromQuery] bool? activeOnly, CancellationToken ct)
    {
        var result = await _expenses.ListCategoriesAsync(activeOnly, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateExpenseCategoryRequest request, CancellationToken ct)
    {
        var result = await _expenses.CreateCategoryAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] string? status, CancellationToken ct)
    {
        var result = await _expenses.ListExpensesAsync(query, status, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _expenses.GetExpenseAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var result = await _expenses.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveExpenseRequest? request, CancellationToken ct)
    {
        var result = await _expenses.ApproveAsync(id, request ?? new ApproveExpenseRequest(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
    {
        var result = await _expenses.PostAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct)
    {
        var result = await _expenses.VoidAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ApiErrors.Fail(ErrorCodes.Validation, "File is required.");

        await using var stream = file.OpenReadStream();
        var result = await _expenses.AddAttachmentAsync(id, stream, file.FileName, file.ContentType, ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
