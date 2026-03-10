using FluentAssertions;
using LogVault.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LogVault.Api.Tests.Integration;

public class IngestAndQueryTests : IClassFixture<LogVaultApiFactory>
{
    private readonly LogVaultApiFactory _factory;

    public IngestAndQueryTests(LogVaultApiFactory factory) => _factory = factory;

    /// <summary>
    /// Verifies the ingest pipeline: API key client POSTs events, background worker commits,
    /// DB read confirms persistence.
    /// </summary>
    [Fact]
    public async Task Ingest_ViaApiKey_WritesToDatabase()
    {
        // Capture baseline count before ingest
        int baselineCount;
        using (var s = _factory.Services.CreateScope())
        {
            var dbBase = s.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            baselineCount = dbBase.LogEvents.Count();
        }

        var ingestClient = await _factory.CreateApiKeyClientAsync("IngestPipelineTest");

        // Use CLEF keys so the parser handles them regardless of casing
        var payload = new
        {
            events = new[]
            {
                new
                {
                    @t = DateTimeOffset.UtcNow.ToString("O"),
                    @l = "Error",
                    @mt = "Pipeline ingest test",
                    @m = "Pipeline ingest test"
                }
            }
        };

        var ingestResponse = await ingestClient.PostAsJsonAsync("/api/ingest/serilog", payload);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Poll up to 5 seconds for the background worker to commit the event
        var committed = false;
        for (var i = 0; i < 10 && !committed; i++)
        {
            await Task.Delay(500);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            committed = db.LogEvents.Count() > baselineCount;
        }

        committed.Should().BeTrue("worker should have committed the event within 5 seconds");
    }

    /// <summary>Verifies the HTTP query endpoint returns seeded events.</summary>
    [Fact]
    public async Task Query_HttpEndpoint_ReturnsSeededEvents()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            db.LogEvents.Add(new LogVault.Domain.Entities.LogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogVault.Domain.Entities.LogLevel.Error,
                MessageTemplate = "HTTP query test",
                RenderedMessage = "HTTP query test",
                PropertiesJson = "{}",
                IngestedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = await _factory.CreateAdminClientAsync();
        var response = await client.GetAsync("/api/logs?level=Error&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Ingest_WithTraceparentHeader_TraceIdStoredOnEvents()
    {
        var client = await _factory.CreateApiKeyClientAsync("TraceTest");
        client.DefaultRequestHeaders.Add("traceparent",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        var payload = new
        {
            events = new[]
            {
                new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = "Information",
                    MessageTemplate = "Trace test",
                    RenderedMessage = "Trace test",
                    Properties = new { }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/ingest/serilog", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            var ev = db.LogEvents.FirstOrDefault(e => e.TraceId == "4bf92f3577b34da6a3ce929d0e0e4736");
            if (ev != null)
            {
                ev.SpanId.Should().Be("00f067aa0ba902b7");
                return;
            }
        }
        Assert.Fail("TraceId not found in DB within 5 seconds");
    }

    [Fact]
    public async Task Ingest_WithXApplicationNameHeader_SourceApplicationPopulated()
    {
        var client = await _factory.CreateApiKeyClientAsync("AppHeaderTest");
        client.DefaultRequestHeaders.Add("X-Application-Name", "MyTestApplication");

        var payload = new
        {
            events = new[]
            {
                new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = "Information",
                    MessageTemplate = "App header test",
                    RenderedMessage = "App header test",
                    Properties = new { }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/ingest/serilog", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            var ev = db.LogEvents.FirstOrDefault(e => e.SourceApplication == "MyTestApplication");
            if (ev != null) return;
        }
        Assert.Fail("Event with SourceApplication=MyTestApplication not found within 5 seconds");
    }
}
