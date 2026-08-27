using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Ersms.Api.Tests;

public class SalesPosApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SalesPosApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
    }

    [Fact]
    public async Task Complete_sale_payment_idempotency_return_and_dashboard()
    {
        await LoginAsync();

        var partRes = await _client.PostAsJsonAsync("/api/v1/parts", new
        {
            sku = "BAT-001",
            name = "Battery",
            unitCost = 400m,
            unitPrice = 800m,
            reorderLevel = 1m
        });
        partRes.EnsureSuccessStatusCode();
        var partId = (await partRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new { quantityDelta = 5m, reason = "Stock" });

        var saleRes = await _client.PostAsJsonAsync("/api/v1/sales", new
        {
            lines = new[] { new { partId, quantity = 2m, unitPrice = 800m } },
            payment = new { amount = 1000m, methodCode = "CASH", idempotencyKey = "sale-pay-1" }
        });
        saleRes.EnsureSuccessStatusCode();
        var sale = await saleRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var saleId = sale.GetProperty("id").GetGuid();
        sale.GetProperty("totalAmount").GetDecimal().Should().Be(1600m);
        sale.GetProperty("amountPaid").GetDecimal().Should().Be(1000m);
        sale.GetProperty("balanceDue").GetDecimal().Should().Be(600m);
        sale.GetProperty("invoice").GetProperty("status").GetString().Should().Be("PARTIAL");

        var partGet = await _client.GetAsync($"/api/v1/parts/{partId}");
        (await partGet.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(3);

        var pay1 = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/payments", new
        {
            amount = 600m,
            methodCode = "CARD",
            idempotencyKey = "pay-dup"
        });
        pay1.EnsureSuccessStatusCode();
        var afterPay = await pay1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        afterPay.GetProperty("balanceDue").GetDecimal().Should().Be(0);
        afterPay.GetProperty("invoice").GetProperty("status").GetString().Should().Be("PAID");

        var payDup = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/payments", new
        {
            amount = 600m,
            methodCode = "CARD",
            idempotencyKey = "pay-dup"
        });
        payDup.EnsureSuccessStatusCode();
        (await payDup.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("payments").GetArrayLength().Should().Be(afterPay.GetProperty("payments").GetArrayLength());

        var lineId = sale.GetProperty("lines").EnumerateArray().First().GetProperty("id").GetGuid();
        var ret = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/returns", new
        {
            lines = new[] { new { saleLineId = lineId, quantity = 1m } },
            refundAmount = 800m,
            refundMethodCode = "CASH",
            idempotencyKey = "ret-1"
        });
        ret.EnsureSuccessStatusCode();

        var afterReturn = await _client.GetAsync($"/api/v1/parts/{partId}");
        (await afterReturn.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(4);

        var dash = await (await _client.GetAsync("/api/v1/dashboard")).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        dash.GetProperty("todaySalesTotal").GetDecimal().Should().BeGreaterThanOrEqualTo(0);
        dash.TryGetProperty("unpaidInvoiceCount", out _).Should().BeTrue();
        dash.GetProperty("unavailable").TryGetProperty("sales", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Sale_rejects_insufficient_stock_and_void_restocks()
    {
        await LoginAsync();

        var partRes = await _client.PostAsJsonAsync("/api/v1/parts", new
        {
            sku = "CAB-001",
            name = "Cable",
            unitCost = 50m,
            unitPrice = 150m,
            reorderLevel = 0m
        });
        partRes.EnsureSuccessStatusCode();
        var partId = (await partRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new { quantityDelta = 1m, reason = "One" });

        var bad = await _client.PostAsJsonAsync("/api/v1/sales", new
        {
            lines = new[] { new { partId, quantity = 5m } }
        });
        bad.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var saleRes = await _client.PostAsJsonAsync("/api/v1/sales", new
        {
            lines = new[] { new { partId, quantity = 1m } }
        });
        saleRes.EnsureSuccessStatusCode();
        var saleId = (await saleRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var voidRes = await _client.PostAsync($"/api/v1/sales/{saleId}/void", null);
        voidRes.EnsureSuccessStatusCode();
        (await voidRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("status").GetString().Should().Be("VOIDED");

        var stock = await _client.GetAsync($"/api/v1/parts/{partId}");
        (await stock.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(1);
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.EnsureSuccessStatusCode();
    }
}
