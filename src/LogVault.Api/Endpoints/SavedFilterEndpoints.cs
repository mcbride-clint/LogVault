using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;

namespace LogVault.Api.Endpoints;

public static class SavedFilterEndpoints
{
    public static IEndpointRouteBuilder MapSavedFilterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/savedfilters").RequireAuthorization("RequireUser");

        group.MapGet("", async (HttpContext ctx, ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var filters = await repo.GetAllAccessibleAsync(ownerId, ct);
            return Results.Ok(filters.Select(f => new SavedFilterDto(f)));
        }).WithName("GetSavedFilters").WithTags("SavedFilters");

        group.MapPost("", async (SavedFilterRequest request, HttpContext ctx,
            ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var filter = new SavedFilter
            {
                Name = request.Name,
                OwnerId = ownerId,
                FilterJson = request.FilterJson,
                IsPublic = request.IsPublic
            };
            filter = await repo.UpsertAsync(filter, ct);
            return Results.Created($"/api/savedfilters/{filter.Id}", new SavedFilterDto(filter));
        }).WithName("CreateSavedFilter").WithTags("SavedFilters");

        group.MapPut("/{id:int}", async (int id, SavedFilterRequest request, HttpContext ctx,
            ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null || existing.OwnerId != ownerId)
                return Results.NotFound();

            existing.Name = request.Name;
            existing.FilterJson = request.FilterJson;
            existing.IsPublic = request.IsPublic;
            await repo.UpsertAsync(existing, ct);
            return Results.Ok(new SavedFilterDto(existing));
        }).WithName("UpdateSavedFilter").WithTags("SavedFilters");

        group.MapDelete("/{id:int}", async (int id, HttpContext ctx,
            ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null || existing.OwnerId != ownerId)
                return Results.NotFound();

            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        }).WithName("DeleteSavedFilter").WithTags("SavedFilters");

        group.MapPatch("/{id:int}/pin", async (int id, HttpContext ctx,
            ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null || existing.OwnerId != ownerId)
                return Results.NotFound();

            existing.IsPinned = !existing.IsPinned;
            await repo.UpsertAsync(existing, ct);
            return Results.Ok(new SavedFilterDto(existing));
        }).WithName("TogglePinSavedFilter").WithTags("SavedFilters");

        group.MapPatch("/{id:int}/visibility", async (int id, HttpContext ctx,
            ISavedFilterRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null || existing.OwnerId != ownerId)
                return Results.NotFound();

            existing.IsPublic = !existing.IsPublic;
            await repo.UpsertAsync(existing, ct);
            return Results.Ok(new SavedFilterDto(existing));
        }).WithName("ToggleVisibilitySavedFilter").WithTags("SavedFilters");

        return app;
    }
}

public record SavedFilterRequest(string Name, string FilterJson, bool IsPublic = false);

public record SavedFilterDto(int Id, string Name, string OwnerId, string FilterJson,
    bool IsPinned, bool IsPublic, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public SavedFilterDto(SavedFilter f) : this(f.Id, f.Name, f.OwnerId, f.FilterJson, f.IsPinned, f.IsPublic, f.CreatedAt, f.UpdatedAt) { }
}
