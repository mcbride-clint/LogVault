using LogVault.Api.Middleware;
using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

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

            // Replace Negotiate (Windows Auth) with a test scheme that auto-authenticates
            // all requests as Admin+User — simulates a domain user in the required AD group.
            // We remove ALL IConfigureOptions<AuthenticationOptions> registrations (including
            // the ones from AddNegotiate()) and start fresh so the Negotiate handler is never
            // resolved — it throws NotSupportedException on the in-memory test server.
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services.RemoveAll<IPostConfigureOptions<AuthenticationOptions>>();
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Replace smtp health check with no-op
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                foreach (var r in options.Registrations.Where(r => r.Name == "smtp").ToList())
                    options.Registrations.Remove(r);
                options.Registrations.Add(new HealthCheckRegistration(
                    "smtp", _ => new NoOpHealthCheck(), null, null));
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

    /// <summary>
    /// Creates an HttpClient authenticated as admin (Admin + User roles).
    /// Adds the <c>X-Test-Auth: true</c> header so <see cref="TestAuthHandler"/> grants access.
    /// </summary>
    public Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.AuthHeader, "true");
        return Task.FromResult(client);
    }

    private sealed class NoOpHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct = default)
            => Task.FromResult(HealthCheckResult.Healthy("mocked"));
    }
}

/// <summary>
/// Test authentication handler that auto-authenticates every request as a domain user
/// with Admin and User roles — replaces Negotiate/Windows auth in integration tests.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    // Requests must carry X-Test-Auth: true to be authenticated.
    // This allows tests that explicitly test unauthenticated/unauthorized behavior to work.
    public const string AuthHeader = "X-Test-Auth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(AuthHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TESTDOMAIN\\testadmin"),
            new Claim("DisplayName", "Test Admin"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
