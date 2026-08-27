using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Ersms.Api.Tests;

public class InventoryPurchasingApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public InventoryPurchasingApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
    }

    [Fact]
    public async Task Part_adjust_po_receive_and_low_stock_dashboard()
    {
        await LoginAsync();

        var partRes = await _client.PostAsJsonAsync("/api/v1/parts", new
        {
            sku = "SCR-001",
            name = "iPhone Screen",
            unitCost = 1500m,
            unitPrice = 2200m,
            reorderLevel = 5m
        });
        partRes.EnsureSuccessStatusCode();
        var part = await partRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var partId = part.GetProperty("id").GetGuid();
        part.GetProperty("quantityOnHand").GetDecimal().Should().Be(0);

        var adjust = await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new
        {
            quantityDelta = 2m,
            reason = "Opening stock"
        });
        adjust.EnsureSuccessStatusCode();
        var afterAdjust = await adjust.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        afterAdjust.GetProperty("quantityOnHand").GetDecimal().Should().Be(2);

        var negative = await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new
        {
            quantityDelta = -10m,
            reason = "Bad"
        });
        negative.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        var supplierRes = await _client.PostAsJsonAsync("/api/v1/suppliers", new
        {
            name = "Parts Co",
            phone = "09171112222"
        });
        supplierRes.EnsureSuccessStatusCode();
        var supplierId = (await supplierRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var poRes = await _client.PostAsJsonAsync("/api/v1/purchase-orders", new
        {
            supplierId,
            lines = new[]
            {
                new { partId, quantityOrdered = 10m, unitCost = 1400m }
            }
        });
        poRes.EnsureSuccessStatusCode();
        var po = await poRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var poId = po.GetProperty("id").GetGuid();
        po.GetProperty("status").GetString().Should().Be("DRAFT");
        var lineId = po.GetProperty("lines").EnumerateArray().First().GetProperty("id").GetGuid();

        var submit = await _client.PostAsync($"/api/v1/purchase-orders/{poId}/submit", null);
        submit.EnsureSuccessStatusCode();
        (await submit.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("status").GetString().Should().Be("ORDERED");

        var receive = await _client.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/receive", new
        {
            lines = new[] { new { lineId, quantity = 10m } }
        });
        receive.EnsureSuccessStatusCode();
        var received = await receive.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        received.GetProperty("status").GetString().Should().Be("RECEIVED");

        var partGet = await _client.GetAsync($"/api/v1/parts/{partId}");
        partGet.EnsureSuccessStatusCode();
        (await partGet.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("quantityOnHand").GetDecimal().Should().Be(12);

        var dashboard = await _client.GetAsync("/api/v1/dashboard");
        dashboard.EnsureSuccessStatusCode();
        var dash = await dashboard.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        dash.GetProperty("lowStockParts").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        dash.TryGetProperty("todaySalesTotal", out _).Should().BeTrue();
        dash.TryGetProperty("unpaidInvoiceCount", out _).Should().BeTrue();
        dash.GetProperty("unavailable").TryGetProperty("expenses", out _).Should().BeTrue();
        dash.GetProperty("unavailable").TryGetProperty("sales", out _).Should().BeFalse();
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.EnsureSuccessStatusCode();
    }
}
