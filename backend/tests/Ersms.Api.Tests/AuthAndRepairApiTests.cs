using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ersms.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ersms.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ErsmsDbContext>));
            services.RemoveAll(typeof(ErsmsDbContext));
            services.AddDbContext<ErsmsDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}

public class AuthAndRepairApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthAndRepairApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    }

    [Fact]
    public async Task Login_with_seed_owner_succeeds()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        me.GetProperty("email").GetString().Should().Be("owner@ersms.local");
        me.GetProperty("permissions").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unauthorized_request_is_rejected()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/customers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Repair_workflow_creates_and_transitions_with_audit()
    {
        await LoginAsync();

        var customerRes = await _client.PostAsJsonAsync("/api/v1/customers", new
        {
            name = "Jane Customer",
            phone = "09171234567",
            email = "jane@example.com"
        });
        customerRes.EnsureSuccessStatusCode();
        var customer = await customerRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var customerId = customer.GetProperty("id").GetGuid();

        var deviceRes = await _client.PostAsJsonAsync("/api/v1/devices", new
        {
            customerId,
            deviceType = "Laptop",
            brand = "Dell",
            model = "Inspiron 15",
            serialNumber = "SN-100"
        });
        deviceRes.EnsureSuccessStatusCode();
        var device = await deviceRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var deviceId = device.GetProperty("id").GetGuid();

        var repairRes = await _client.PostAsJsonAsync("/api/v1/repairs", new
        {
            customerId,
            deviceId,
            reportedProblem = "Will not power on",
            condition = "Minor scratches",
            accessories = "Charger",
            serviceLines = new[]
            {
                new { serviceId = (Guid?)null, serviceName = "Diagnosis", quantity = 1m, unitPrice = 500m, discount = 0m }
            }
        });
        repairRes.EnsureSuccessStatusCode();
        var repair = await repairRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        repair.GetProperty("statusCode").GetString().Should().Be("RECEIVED");
        var repairId = repair.GetProperty("id").GetGuid();

        var statusRes = await _client.PatchAsJsonAsync($"/api/v1/repairs/{repairId}/status", new { statusCode = "DIAGNOSIS", reason = "Started" });
        statusRes.EnsureSuccessStatusCode();
        var updated = await statusRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("statusCode").GetString().Should().Be("DIAGNOSIS");
        updated.GetProperty("statusHistory").EnumerateArray().Count().Should().BeGreaterThan(1);

        var lastHistory = updated.GetProperty("statusHistory").EnumerateArray().Last();
        lastHistory.GetProperty("previousStatusName").GetString().Should().Be("Received");
        lastHistory.GetProperty("newStatusName").GetString().Should().Be("Diagnosis");
        lastHistory.GetProperty("previousStatusCode").GetString().Should().Be("RECEIVED");
        lastHistory.GetProperty("newStatusCode").GetString().Should().Be("DIAGNOSIS");

        var bad = await _client.PatchAsJsonAsync($"/api/v1/repairs/{repairId}/status", new { statusCode = "COMPLETED", reason = "skip" });
        bad.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var audit = await _client.GetAsync("/api/v1/audit-logs?page=1&pageSize=50");
        audit.EnsureSuccessStatusCode();
        var auditBody = await audit.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        auditBody.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Repairing_allowed_next_prefers_testing_and_single_history_per_patch()
    {
        await LoginAsync();

        var customerRes = await _client.PostAsJsonAsync("/api/v1/customers", new
        {
            name = "Parts Loop Customer",
            phone = "09171112233",
            email = "parts-loop@example.com"
        });
        customerRes.EnsureSuccessStatusCode();
        var customerId = (await customerRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var deviceRes = await _client.PostAsJsonAsync("/api/v1/devices", new
        {
            customerId,
            deviceType = "Phone",
            brand = "Samsung",
            model = "A54"
        });
        deviceRes.EnsureSuccessStatusCode();
        var deviceId = (await deviceRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var repairRes = await _client.PostAsJsonAsync("/api/v1/repairs", new
        {
            customerId,
            deviceId,
            reportedProblem = "Battery swell"
        });
        repairRes.EnsureSuccessStatusCode();
        var repairId = (await repairRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        foreach (var code in new[] { "DIAGNOSIS", "APPROVED", "REPAIRING" })
        {
            var step = await _client.PatchAsJsonAsync($"/api/v1/repairs/{repairId}/status", new { statusCode = code, reason = $"to {code}" });
            step.EnsureSuccessStatusCode();
        }

        var detail = await _client.GetAsync($"/api/v1/repairs/{repairId}");
        detail.EnsureSuccessStatusCode();
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("statusCode").GetString().Should().Be("REPAIRING");

        var allowed = body.GetProperty("allowedNextStatuses").EnumerateArray()
            .Select(x => x.GetProperty("code").GetString())
            .ToList();
        allowed[0].Should().Be("TESTING");
        allowed.Should().Contain("WAITING_FOR_PARTS");
        allowed.Should().Contain("CANCELLED");

        var historyBefore = body.GetProperty("statusHistory").GetArrayLength();
        var patch = await _client.PatchAsJsonAsync($"/api/v1/repairs/{repairId}/status", new
        {
            statusCode = "WAITING_FOR_PARTS",
            reason = "Need board"
        });
        patch.EnsureSuccessStatusCode();
        var after = await patch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        after.GetProperty("statusCode").GetString().Should().Be("WAITING_FOR_PARTS");
        after.GetProperty("statusHistory").GetArrayLength().Should().Be(historyBefore + 1);
        after.GetProperty("allowedNextStatuses").EnumerateArray().First().GetProperty("code").GetString()
            .Should().Be("REPAIRING");
    }

    [Fact]
    public async Task Customer_update_deactivate_and_allowed_repair_statuses()
    {
        await LoginAsync();

        var create = await _client.PostAsJsonAsync("/api/v1/customers", new
        {
            name = "Edit Me",
            email = "edit@example.com",
            phone = "09170001111"
        });
        create.EnsureSuccessStatusCode();
        var customer = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var customerId = customer.GetProperty("id").GetGuid();
        customer.GetProperty("email").GetString().Should().Be("edit@example.com");
        customer.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var patch = await _client.PatchAsJsonAsync($"/api/v1/customers/{customerId}", new
        {
            name = "Edited Name",
            email = "edited@example.com",
            phone = "09170002222"
        });
        patch.EnsureSuccessStatusCode();
        var updated = await patch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("name").GetString().Should().Be("Edited Name");
        updated.GetProperty("email").GetString().Should().Be("edited@example.com");

        var deactivate = await _client.PostAsync($"/api/v1/customers/{customerId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();
        var inactive = await deactivate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        inactive.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var listActive = await _client.GetAsync("/api/v1/customers?pageSize=100");
        listActive.EnsureSuccessStatusCode();
        var activeBody = await listActive.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        activeBody.GetProperty("items").EnumerateArray()
            .Any(x => x.GetProperty("id").GetGuid() == customerId)
            .Should().BeFalse();

        var listAll = await _client.GetAsync("/api/v1/customers?pageSize=100&includeInactive=true");
        listAll.EnsureSuccessStatusCode();
        var allBody = await listAll.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        allBody.GetProperty("items").EnumerateArray()
            .Any(x => x.GetProperty("id").GetGuid() == customerId)
            .Should().BeTrue();

        var activate = await _client.PostAsync($"/api/v1/customers/{customerId}/activate", null);
        activate.EnsureSuccessStatusCode();

        var deviceRes = await _client.PostAsJsonAsync("/api/v1/devices", new
        {
            customerId,
            deviceType = "Phone",
            brand = "Apple",
            model = "iPhone"
        });
        deviceRes.EnsureSuccessStatusCode();
        var deviceId = (await deviceRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var repairRes = await _client.PostAsJsonAsync("/api/v1/repairs", new
        {
            customerId,
            deviceId,
            reportedProblem = "Screen crack"
        });
        repairRes.EnsureSuccessStatusCode();
        var repair = await repairRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var codes = repair.GetProperty("allowedNextStatuses").EnumerateArray()
            .Select(x => x.GetProperty("code").GetString())
            .ToList();
        codes.Should().Contain("DIAGNOSIS");
        codes.Should().Contain("CANCELLED");
        codes.Should().NotContain("COMPLETED");
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.EnsureSuccessStatusCode();
    }
}
