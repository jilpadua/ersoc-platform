using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ersms.Api.Tests;

public class AccountingApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AccountingApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "owner@ersms.local",
            password = "Owner123!"
        });
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Chart_of_accounts_and_periods_are_seeded()
    {
        await LoginAsync();
        var accounts = await _client.GetAsync("/api/v1/accounts");
        accounts.EnsureSuccessStatusCode();
        var list = await accounts.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        list.GetArrayLength().Should().BeGreaterThan(5);

        var periods = await _client.GetAsync("/api/v1/accounting/periods");
        periods.EnsureSuccessStatusCode();
        var periodList = await periods.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        periodList.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Sale_posts_balanced_journal_and_is_idempotent()
    {
        await LoginAsync();

        var partRes = await _client.PostAsJsonAsync("/api/v1/parts", new
        {
            sku = $"ACC-{Guid.NewGuid():N}".Substring(0, 12),
            name = "Accounting Test Part",
            unitCost = 40m,
            unitPrice = 100m,
            reorderLevel = 1m
        });
        partRes.EnsureSuccessStatusCode();
        var partId = (await partRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/v1/parts/{partId}/adjustments", new
        {
            quantityDelta = 10m,
            reason = "seed stock"
        });

        var saleRes = await _client.PostAsJsonAsync("/api/v1/sales", new
        {
            lines = new[] { new { partId, quantity = 1m, unitPrice = 100m, discount = 0m } },
            payment = new { amount = 100m, methodCode = "CASH", idempotencyKey = $"pay-{Guid.NewGuid():N}" }
        });
        saleRes.EnsureSuccessStatusCode();
        var sale = await saleRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var saleId = sale.GetProperty("id").GetGuid();

        var journalRes = await _client.GetAsync($"/api/v1/journals/by-source?sourceType=SaleCompleted&sourceId={saleId}");
        journalRes.EnsureSuccessStatusCode();
        var journal = await journalRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = journal.GetProperty("lines").EnumerateArray().ToList();
        var debits = lines.Sum(l => l.GetProperty("debit").GetDecimal());
        var credits = lines.Sum(l => l.GetProperty("credit").GetDecimal());
        debits.Should().Be(credits);
        debits.Should().BeGreaterThan(0);

        var again = await _client.GetAsync($"/api/v1/journals/by-source?sourceType=SaleCompleted&sourceId={saleId}");
        again.EnsureSuccessStatusCode();
        var againBody = await again.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        againBody.GetProperty("id").GetGuid().Should().Be(journal.GetProperty("id").GetGuid());

        var asOf = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
        var tb = await _client.GetAsync($"/api/v1/accounting/reports/trial-balance?asOf={asOf}");
        tb.EnsureSuccessStatusCode();
        var tbBody = await tb.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var rows = tbBody.EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        var totalDebit = rows.Sum(r => r.GetProperty("debitTotal").GetDecimal());
        var totalCredit = rows.Sum(r => r.GetProperty("creditTotal").GetDecimal());
        totalDebit.Should().Be(totalCredit);
    }

    [Fact]
    public async Task Closed_period_blocks_manual_journal()
    {
        await LoginAsync();
        var periods = await _client.GetAsync("/api/v1/accounting/periods");
        periods.EnsureSuccessStatusCode();
        var periodList = await periods.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var period = periodList.EnumerateArray().First();
        var periodId = period.GetProperty("id").GetGuid();
        var start = period.GetProperty("startDate").GetString()!;

        await _client.PostAsync($"/api/v1/accounting/periods/{periodId}/close", null);

        var accounts = await _client.GetAsync("/api/v1/accounts");
        var accountList = await accounts.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var cash = accountList.EnumerateArray().First(a => a.GetProperty("code").GetString() == "1000");
        var revenue = accountList.EnumerateArray().First(a => a.GetProperty("code").GetString() == "4000");

        var entryDate = DateTimeOffset.Parse(start + "T12:00:00Z");
        var post = await _client.PostAsJsonAsync("/api/v1/journals/manual", new
        {
            entryDate,
            memo = "should fail",
            lines = new[]
            {
                new { accountId = cash.GetProperty("id").GetGuid(), debit = 10m, credit = 0m, description = "dr" },
                new { accountId = revenue.GetProperty("id").GetGuid(), debit = 0m, credit = 10m, description = "cr" }
            }
        });
        post.IsSuccessStatusCode.Should().BeFalse();

        await _client.PostAsync($"/api/v1/accounting/periods/{periodId}/reopen", null);
    }
}
