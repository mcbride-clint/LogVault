using FluentAssertions;
using LogVault.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace LogVault.Api.Tests.Integration;

public class ExportTests : IClassFixture<LogVaultApiFactory>
{
    private readonly LogVaultApiFactory _factory;

    public ExportTests(LogVaultApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_Json_ReturnsValidJsonArray()
    {
        // Seed a log event directly via DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
            db.LogEvents.Add(new LogVault.Domain.Entities.LogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogVault.Domain.Entities.LogLevel.Warning,
                MessageTemplate = "Export test event",
                RenderedMessage = "Export test event",
                PropertiesJson = "{}",
                IngestedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = await _factory.CreateAdminClientAsync();
        var response = await client.GetAsync("/api/logs/export?format=json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.TrimStart().Should().StartWith("[");
        content.TrimEnd().Should().EndWith("]");
    }

    [Fact]
    public async Task Export_Csv_ReturnsValidCsv()
    {
        var client = await _factory.CreateAdminClientAsync();
        var response = await client.GetAsync("/api/logs/export?format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var content = await response.Content.ReadAsStringAsync();
        // CSV must include header row
        content.Should().Contain("Timestamp");
        content.Should().Contain("Level");
        content.Should().Contain("RenderedMessage");
    }
}
