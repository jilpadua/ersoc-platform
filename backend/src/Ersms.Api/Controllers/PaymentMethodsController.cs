using Ersms.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ersms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/payment-methods")]
public sealed class PaymentMethodsController : ControllerBase
{
    private readonly ISaleService _sales;

    public PaymentMethodsController(ISaleService sales) => _sales = sales;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var result = await _sales.ListPaymentMethodsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ApiErrors.FromResult(result);
    }
}
