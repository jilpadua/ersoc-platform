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

        var bad = await _client.PatchAsJsonAsync($"/api/v1/repairs/{repairId}/status", new { statusCode = "COMPLETED", reason = "skip" });
        bad.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var audit = await _client.GetAsync("/api/v1/audit-logs?page=1&pageSize=50");
        audit.EnsureSuccessStatusCode();
        var auditBody = await audit.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        auditBody.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@ersms.local", password = "Owner123!" });
        login.EnsureSuccessStatusCode();
    }
}
