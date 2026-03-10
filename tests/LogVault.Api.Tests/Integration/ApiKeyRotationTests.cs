using FluentAssertions;
using LogVault.Api.Middleware;
using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LogVault.Api.Tests.Integration;

public class ApiKeyRotationTests : IClassFixture<LogVaultApiFactory>
{
    private readonly LogVaultApiFactory _factory;

    public ApiKeyRotationTests(LogVaultApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RotateApiKey_OldKeyStillValidWithinGrace()
    {
        // Create an API key directly
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var oldRawKey = ApiKeyMiddleware.GenerateRawKey();
        var oldHash = ApiKeyMiddleware.ComputeSha256(oldRawKey);
        var oldKey = await repo.CreateAsync(new ApiKey
        {
            KeyHash = oldHash,
            Label = "RotateOldKey",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Verify old key works for ingestion
        var ingestClient = _factory.CreateClient();
        ingestClient.DefaultRequestHeaders.Add("X-Api-Key", oldRawKey);

        var payload = new { events = new[] { new { Timestamp = DateTimeOffset.UtcNow, Level = "Information", MessageTemplate = "t", RenderedMessage = "t", Properties = new { } } } };
        var ingestResult = await ingestClient.PostAsJsonAsync("/api/ingest/serilog", payload);
        ingestResult.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // The old key's ExpiresAt is still null (not rotated yet) — it should work
        var found = await repo.FindByHashAsync(oldHash);
        found.Should().NotBeNull();
        found!.IsUsable.Should().BeTrue();
    }

    [Fact]
    public async Task RotateApiKey_ExpiredKeyIsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var oldRawKey = ApiKeyMiddleware.GenerateRawKey();
        var oldHash = ApiKeyMiddleware.ComputeSha256(oldRawKey);
        var oldKey = await repo.CreateAsync(new ApiKey
        {
            KeyHash = oldHash,
            Label = "ExpiredKey",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            // Set ExpiresAt in the past to simulate an expired rotated key
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        });

        var ingestClient = _factory.CreateClient();
        ingestClient.DefaultRequestHeaders.Add("X-Api-Key", oldRawKey);

        var payload = new { events = new[] { new { Timestamp = DateTimeOffset.UtcNow, Level = "Information", MessageTemplate = "t", RenderedMessage = "t", Properties = new { } } } };
        var ingestResult = await ingestClient.PostAsJsonAsync("/api/ingest/serilog", payload);

        // Expired key should not authenticate; CanIngest policy should reject
        ingestResult.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
