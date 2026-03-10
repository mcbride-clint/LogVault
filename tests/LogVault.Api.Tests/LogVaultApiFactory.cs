using LogVault.Api.Middleware;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using LogVault.Domain.Services;
using LogVault.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using System.Net.Http.Json;

namespace LogVault.Api.Tests;

public class LogVaultApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Use a temporary file-based SQLite database so background worker threads can write
    // concurrently without the single-connection constraint of in-memory SQLite.
    private string? _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LogVaultDbContext>>();
            services.RemoveAll<LogVaultDbContext>();
            services.AddDbContext<LogVaultDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

            // Mock LDAP auth — accept test credentials admin/password → Admin+User roles
            services.RemoveAll<IAdAuthService>();
            var adMock = new Mock<IAdAuthService>();
            adMock.Setup(s => s.AuthenticateAsync("admin", "password", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AdAuthResult(true, "Test Admin", "admin@test.local", null));
            adMock.Setup(s => s.GetGroupsAsync("admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "LogVault-Admins" });
            services.AddSingleton(adMock.Object);

            // Replace smtp/ldap health checks with no-ops
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                foreach (var r in options.Registrations.Where(r => r.Name is "smtp" or "ldap").ToList())
                    options.Registrations.Remove(r);
                options.Registrations.Add(new HealthCheckRegistration(
                    "smtp", _ => new NoOpHealthCheck(), null, null));
                options.Registrations.Add(new HealthCheckRegistration(
                    "ldap", _ => new NoOpHealthCheck(), null, null));
            });
        });
    }

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"logvault_test_{Guid.NewGuid():N}.db");

        // Trigger app startup + warm up so that background services (LogIngestionWorker) are running
        using var warmupClient = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await warmupClient.GetAsync("/health");

        // Allow time for background services to start their event loops
        await Task.Delay(1000);

        // Ensure schema exists (Program.cs already ran MigrateAsync, but be safe)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_dbPath != null)
        {
            TryDelete(_dbPath);
            TryDelete(_dbPath + "-shm");
            TryDelete(_dbPath + "-wal");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
    }

    /// <summary>Creates an HttpClient pre-configured with a new API key in X-Api-Key header.</summary>
    public async Task<HttpClient> CreateApiKeyClientAsync(string label = "TestKey")
    {
        using var scope = Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var rawKey = ApiKeyMiddleware.GenerateRawKey();
        var keyHash = ApiKeyMiddleware.ComputeSha256(rawKey);
        await repo.CreateAsync(new ApiKey
        {
            KeyHash = keyHash,
            Label = label,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        return client;
    }

    /// <summary>Creates an HttpClient authenticated as admin (cookie auth via /api/auth/login).</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "password", RememberMe = false });

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Login failed: {response.StatusCode}");

        return client;
    }

    private sealed class NoOpHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct = default)
            => Task.FromResult(HealthCheckResult.Healthy("mocked"));
    }
}
