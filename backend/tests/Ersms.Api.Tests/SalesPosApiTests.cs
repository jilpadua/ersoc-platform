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
        sale.GetProperty("lines").EnumerateArray().First().GetProperty("unitCost").GetDecimal().Should().Be(400m);
        sale.GetProperty("invoice").TryGetProperty("issuedAt", out _).Should().BeTrue();
        sale.GetProperty("invoice").TryGetProperty("createdAt", out _).Should().BeTrue();
        sale.GetProperty("completedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        var paidSum = sale.GetProperty("payments").EnumerateArray().Sum(p => p.GetProperty("amount").GetDecimal());
        sale.GetProperty("amountPaid").GetDecimal().Should().Be(paidSum);

        var ledger = await (await _client.GetAsync($"/api/v1/parts/{partId}/ledger")).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var saleLedger = ledger.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("entryType").GetString() == "Sale" && e.GetProperty("referenceId").GetGuid() == saleId)
            .ToList();
        saleLedger.Should().ContainSingle();
        saleLedger[0].GetProperty("quantityDelta").GetDecimal().Should().Be(-2m);

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
        afterPay.GetProperty("amountPaid").GetDecimal()
            .Should().Be(afterPay.GetProperty("payments").EnumerateArray().Sum(p => p.GetProperty("amount").GetDecimal()));

        var payDup = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/payments", new
        {
            amount = 600m,
            methodCode = "CARD",
            idempotencyKey = "pay-dup"
        });
        payDup.EnsureSuccessStatusCode();
        (await payDup.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("payments").GetArrayLength().Should().Be(afterPay.GetProperty("payments").GetArrayLength());

        var overpay = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/payments", new
        {
            amount = 1m,
            methodCode = "CASH",
            idempotencyKey = "overpay-1"
        });
        overpay.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        var lineId = sale.GetProperty("lines").EnumerateArray().First().GetProperty("id").GetGuid();
        var ret = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/returns", new
        {
            lines = new[] { new { saleLineId = lineId, quantity = 1m } },
            refundAmount = 800m,
            refundMethodCode = "CASH",
            idempotencyKey = "ret-1"
        });
        ret.EnsureSuccessStatusCode();
        var afterRet = await ret.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        afterRet.GetProperty("amountPaid").GetDecimal()
            .Should().Be(afterRet.GetProperty("payments").EnumerateArray().Sum(p => p.GetProperty("amount").GetDecimal()));
        afterRet.GetProperty("returns").EnumerateArray().First().TryGetProperty("refundedAt", out var refundedAt).Should().BeTrue();
        refundedAt.ValueKind.Should().NotBe(JsonValueKind.Null);

        var afterReturn = await _client.GetAsync($"/api/v1/parts/{partId}");
        (await afterReturn.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(4);

        var dash = await (await _client.GetAsync("/api/v1/dashboard")).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        dash.GetProperty("todaySalesTotal").GetDecimal().Should().BeGreaterThanOrEqualTo(0);
        dash.TryGetProperty("unpaidInvoiceCount", out _).Should().BeTrue();
        dash.TryGetProperty("todayExpenseTotal", out _).Should().BeTrue();
        dash.TryGetProperty("cashAndBankBalance", out _).Should().BeTrue();
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
        var voided = await voidRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        voided.GetProperty("status").GetString().Should().Be("VOIDED");
        voided.TryGetProperty("voidedAt", out var voidedAt).Should().BeTrue();
        voidedAt.ValueKind.Should().NotBe(JsonValueKind.Null);
        voided.GetProperty("invoice").TryGetProperty("voidedAt", out var invVoided).Should().BeTrue();
        invVoided.ValueKind.Should().NotBe(JsonValueKind.Null);

        var payVoided = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/payments", new
        {
            amount = 10m,
            methodCode = "CASH",
            idempotencyKey = "pay-voided"
        });
        payVoided.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.BadRequest, System.Net.HttpStatusCode.Conflict);

        var stock = await _client.GetAsync($"/api/v1/parts/{partId}");
        (await stock.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(1);
    }

    [Fact]
    public async Task Return_then_void_is_rejected_and_over_return_fails()
    {
        await LoginAsync();

        var partRes = await _client.PostAsJsonAsync("/api/v1/parts", new
        {
            sku = "SCR-H1",
            name = "Screen Hardening",
            unitCost = 100m,
            unitPrice = 200m,
            reorderLevel = 0m
        });
        partRes.EnsureSuccessStatusCode();
        var partId = (await partRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new { quantityDelta = 5m, reason = "Stock" });

        var saleRes = await _client.PostAsJsonAsync("/api/v1/sales", new
        {
            lines = new[] { new { partId, quantity = 2m } }
        });
        saleRes.EnsureSuccessStatusCode();
        var sale = await saleRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var saleId = sale.GetProperty("id").GetGuid();
        var lineId = sale.GetProperty("lines").EnumerateArray().First().GetProperty("id").GetGuid();

        var overReturn = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/returns", new
        {
            lines = new[]
            {
                new { saleLineId = lineId, quantity = 1m },
                new { saleLineId = lineId, quantity = 2m }
            },
            refundAmount = 0m
        });
        overReturn.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        var ret = await _client.PostAsJsonAsync($"/api/v1/sales/{saleId}/returns", new
        {
            lines = new[] { new { saleLineId = lineId, quantity = 1m } },
            refundAmount = 0m
        });
        ret.EnsureSuccessStatusCode();

        var stockAfterReturn = (await (await _client.GetAsync($"/api/v1/parts/{partId}")).Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal();
        stockAfterReturn.Should().Be(4);

        var voidRes = await _client.PostAsync($"/api/v1/sales/{saleId}/void", null);
        voidRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var stockAfterVoidAttempt = (await (await _client.GetAsync($"/api/v1/parts/{partId}")).Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal();
        stockAfterVoidAttempt.Should().Be(4);
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.EnsureSuccessStatusCode();
    }
}
