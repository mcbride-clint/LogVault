using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;

namespace LogVault.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboards").RequireAuthorization("RequireUser");

        // List caller's dashboards
        group.MapGet("", async (HttpContext ctx, IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboards = await repo.GetByOwnerAsync(ownerId, ct);
            return Results.Ok(dashboards.Select(d => new DashboardDto(d)));
        }).WithName("GetDashboards").WithTags("Dashboards");

        // Get or create default dashboard
        group.MapGet("/default", async (HttpContext ctx, IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = await repo.GetOrCreateDefaultAsync(ownerId, ct);
            return Results.Ok(new DashboardDto(dashboard));
        }).WithName("GetDefaultDashboard").WithTags("Dashboards");

        // Get specific dashboard
        group.MapGet("/{id:int}", async (int id, HttpContext ctx, IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = await repo.GetByIdAsync(id, ct);
            if (dashboard == null) return Results.NotFound();
            if (dashboard.OwnerId != ownerId) return Results.Forbid();
            return Results.Ok(new DashboardDto(dashboard));
        }).WithName("GetDashboardById").WithTags("Dashboards");

        // Create dashboard
        group.MapPost("", async (UpsertDashboardRequest request, HttpContext ctx,
            IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = new Dashboard
            {
                Name = request.Name,
                OwnerId = ownerId,
                IsDefault = request.IsDefault,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dashboard = await repo.CreateAsync(dashboard, ct);
            return Results.Created($"/api/dashboards/{dashboard.Id}", new DashboardDto(dashboard));
        }).WithName("CreateDashboard").WithTags("Dashboards");

        // Update dashboard name/isDefault
        group.MapPut("/{id:int}", async (int id, UpsertDashboardRequest request, HttpContext ctx,
            IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = await repo.GetByIdAsync(id, ct);
            if (dashboard == null) return Results.NotFound();
            if (dashboard.OwnerId != ownerId) return Results.Forbid();

            dashboard.Name = request.Name;
            dashboard.IsDefault = request.IsDefault;
            await repo.UpdateAsync(dashboard, ct);
            return Results.Ok(new DashboardDto(dashboard));
        }).WithName("UpdateDashboard").WithTags("Dashboards");

        // Delete dashboard (reject if last one)
        group.MapDelete("/{id:int}", async (int id, HttpContext ctx,
            IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = await repo.GetByIdAsync(id, ct);
            if (dashboard == null) return Results.NotFound();
            if (dashboard.OwnerId != ownerId) return Results.Forbid();

            var count = await repo.CountByOwnerAsync(ownerId, ct);
            if (count <= 1)
                return Results.BadRequest("Cannot delete your last dashboard.");

            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        }).WithName("DeleteDashboard").WithTags("Dashboards");

        // Replace widget list for a dashboard (full reorder/update)
        group.MapPut("/{id:int}/widgets", async (int id, UpsertWidgetsRequest request, HttpContext ctx,
            IDashboardRepository repo, CancellationToken ct) =>
        {
            var ownerId = ctx.User.Identity?.Name ?? "";
            var dashboard = await repo.GetByIdAsync(id, ct);
            if (dashboard == null) return Results.NotFound();
            if (dashboard.OwnerId != ownerId) return Results.Forbid();

            // Replace widget collection
            dashboard.Widgets.Clear();
            foreach (var spec in request.Widgets.OrderBy(w => w.SortOrder))
            {
                dashboard.Widgets.Add(new DashboardWidget
                {
                    WidgetType = spec.WidgetType,
                    Title = spec.Title,
                    SortOrder = spec.SortOrder,
                    ConfigJson = spec.ConfigJson
                });
            }
            await repo.UpdateAsync(dashboard, ct);
            return Results.Ok(new DashboardDto(dashboard));
        }).WithName("UpdateDashboardWidgets").WithTags("Dashboards");

        return app;
    }
}

public record UpsertDashboardRequest(string Name, bool IsDefault);
public record UpsertWidgetsRequest(List<WidgetSpec> Widgets);
public record WidgetSpec(string WidgetType, string Title, int SortOrder, string ConfigJson);

public record DashboardWidgetDto(int Id, string WidgetType, string Title, int SortOrder, string ConfigJson)
{
    public DashboardWidgetDto(DashboardWidget w)
        : this(w.Id, w.WidgetType, w.Title, w.SortOrder, w.ConfigJson) { }
}

public record DashboardDto(int Id, string Name, string OwnerId, bool IsDefault,
    DateTimeOffset CreatedAt, List<DashboardWidgetDto> Widgets)
{
    public DashboardDto(Dashboard d)
        : this(d.Id, d.Name, d.OwnerId, d.IsDefault, d.CreatedAt,
               d.Widgets.OrderBy(w => w.SortOrder).Select(w => new DashboardWidgetDto(w)).ToList()) { }
}
