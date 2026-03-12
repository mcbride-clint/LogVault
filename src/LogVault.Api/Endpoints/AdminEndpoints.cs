using LogVault.Api.Middleware;
using LogVault.Application.Workers;
using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;

namespace LogVault.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("RequireAdmin");

        group.MapGet("/applications", async (ILogEventRepository repo, CancellationToken ct) =>
        {
            var apps = await repo.GetDistinctApplicationsAsync(ct);
            return Results.Ok(apps);
        }).WithName("GetApplications").WithTags("Admin");

        group.MapDelete("/logs/purge", async (
            DateTimeOffset? before,
            ILogEventRepository repo,
            CancellationToken ct) =>
        {
            if (!before.HasValue)
                return Results.BadRequest(new { error = "before parameter is required" });

            var deleted = await repo.DeleteOlderThanAsync(before.Value, ct);
            return Results.Ok(new { deletedCount = deleted });
        }).WithName("PurgeLogs").WithTags("Admin");

        group.MapPost("/logs/purge/trigger", async (RetentionWorker worker, CancellationToken ct) =>
        {
            var deleted = await worker.RunRetentionAsync(ct);
            return Results.Ok(new { deletedCount = deleted });
        }).WithName("TriggerRetention").WithTags("Admin");

        // API Key management
        var keyGroup = group.MapGroup("/apikeys");

        keyGroup.MapGet("", async (IApiKeyRepository repo, CancellationToken ct) =>
        {
            var keys = await repo.GetAllAsync(ct);
            return Results.Ok(keys.Select(k => new ApiKeyDto(k)));
        }).WithName("GetApiKeys").WithTags("Admin");

        keyGroup.MapPost("", async (CreateApiKeyRequest request, IApiKeyRepository repo, CancellationToken ct) =>
        {
            var rawKey = ApiKeyMiddleware.GenerateRawKey();
            var hash = ApiKeyMiddleware.ComputeSha256(rawKey);

            var key = new ApiKey
            {
                KeyHash = hash,
                Label = request.Label,
                DefaultApplication = request.DefaultApplication,
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            key = await repo.CreateAsync(key, ct);
            return Results.Created($"/api/admin/apikeys/{key.Id}", new
            {
                key.Id,
                RawKey = rawKey, // shown once
                key.Label,
                key.DefaultApplication
            });
        }).WithName("CreateApiKey").WithTags("Admin");

        keyGroup.MapPost("/{id:int}/rotate", async (int id, IApiKeyRepository repo,
            Microsoft.Extensions.Configuration.IConfiguration config, CancellationToken ct) =>
        {
            var rawKey = ApiKeyMiddleware.GenerateRawKey();
            var hash = ApiKeyMiddleware.ComputeSha256(rawKey);
            var graceHours = int.TryParse(config["ApiKeys:RotationGraceHours"], out var g) ? g : 24;

            var newKey = new ApiKey
            {
                KeyHash = hash,
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                var (created, expired) = await repo.RotateAsync(id, newKey, ct);
                return Results.Ok(new
                {
                    NewKeyId = created.Id,
                    RawKey = rawKey,
                    Label = created.Label,
                    OldKeyExpiresAt = expired.ExpiresAt
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).WithName("RotateApiKey").WithTags("Admin");

        keyGroup.MapDelete("/{id:int}", async (int id, IApiKeyRepository repo, CancellationToken ct) =>
        {
            await repo.RevokeAsync(id, ct);
            return Results.NoContent();
        }).WithName("RevokeApiKey").WithTags("Admin");

        return app;
    }
}

public record CreateApiKeyRequest(string Label, string? DefaultApplication);

public record ApiKeyDto(int Id, string Label, string? DefaultApplication, bool IsEnabled, bool IsRevoked,
    DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, int? RotatedFromId)
{
    public ApiKeyDto(ApiKey k) : this(k.Id, k.Label, k.DefaultApplication, k.IsEnabled, k.IsRevoked,
        k.CreatedAt, k.ExpiresAt, k.RotatedFromId) { }
}
