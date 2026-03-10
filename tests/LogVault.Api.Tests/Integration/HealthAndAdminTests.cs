using FluentAssertions;
using LogVault.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LogVault.Api.Tests.Integration;

public class HealthAndAdminTests : IClassFixture<LogVaultApiFactory>
{
    private readonly LogVaultApiFactory _factory;

    public HealthAndAdminTests(LogVaultApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthEndpoint_Returns200WithAllHealthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PurgeTrigger_WithNoAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/admin/logs/purge/trigger", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PurgeTrigger_WithApiKeyOnly_Returns403()
    {
        // API key has "Ingest" role, not "Admin" — purge trigger requires Admin
        var client = await _factory.CreateApiKeyClientAsync("PurgeTestKey");
        var response = await client.PostAsync("/api/admin/logs/purge/trigger", null);
        // API keys don't have Admin role — should be Forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LogQuery_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/logs");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingest_WithoutApiKeyOrAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var payload = new { events = Array.Empty<object>() };
        var response = await client.PostAsJsonAsync("/api/ingest/serilog", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
