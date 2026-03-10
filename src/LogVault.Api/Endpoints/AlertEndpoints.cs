using LogVault.Application.Alerts;
using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using System.Security.Claims;

namespace LogVault.Api.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alerts").RequireAuthorization("RequireUser");

        group.MapGet("", async (ClaimsPrincipal user, IAlertRuleRepository repo, CancellationToken ct) =>
        {
            var ownerId = GetUserId(user);
            var rules = user.IsInRole("Admin")
                ? await repo.GetAllEnabledAsync(ct)
                : await repo.GetByOwnerAsync(ownerId, ct);
            return Results.Ok(rules);
        }).WithName("GetAlerts").WithTags("Alerts");

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IAlertRuleRepository repo, CancellationToken ct) =>
        {
            var rule = await repo.GetByIdAsync(id, ct);
            if (rule is null) return Results.NotFound();
            if (!user.IsInRole("Admin") && rule.OwnerId != GetUserId(user)) return Results.Forbid();
            return Results.Ok(rule);
        }).WithName("GetAlertById").WithTags("Alerts");

        group.MapPost("", async (AlertRuleRequest request, ClaimsPrincipal user,
            IAlertRuleRepository repo, AlertRuleExpressionCache cache, CancellationToken ct) =>
        {
            ValidateExpression(request.FilterExpression);
            var rule = MapToRule(request, GetUserId(user));
            rule = await repo.UpsertAsync(rule, ct);
            return Results.Created($"/api/alerts/{rule.Id}", rule);
        }).WithName("CreateAlert").WithTags("Alerts");

        group.MapPut("/{id:int}", async (int id, AlertRuleRequest request, ClaimsPrincipal user,
            IAlertRuleRepository repo, AlertRuleExpressionCache cache, CancellationToken ct) =>
        {
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (!user.IsInRole("Admin") && existing.OwnerId != GetUserId(user)) return Results.Forbid();

            ValidateExpression(request.FilterExpression);
            var rule = MapToRule(request, existing.OwnerId);
            rule.Id = id;
            rule = await repo.UpsertAsync(rule, ct);
            cache.Invalidate(id);
            return Results.Ok(rule);
        }).WithName("UpdateAlert").WithTags("Alerts");

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user,
            IAlertRuleRepository repo, AlertRuleExpressionCache cache, CancellationToken ct) =>
        {
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (!user.IsInRole("Admin") && existing.OwnerId != GetUserId(user)) return Results.Forbid();

            await repo.DeleteAsync(id, ct);
            cache.Invalidate(id);
            return Results.NoContent();
        }).WithName("DeleteAlert").WithTags("Alerts");

        group.MapGet("/{id:int}/history", async (int id, ClaimsPrincipal user,
            IAlertRuleRepository ruleRepo, IAlertFiredRepository firedRepo, CancellationToken ct) =>
        {
            var rule = await ruleRepo.GetByIdAsync(id, ct);
            if (rule is null) return Results.NotFound();
            if (!user.IsInRole("Admin") && rule.OwnerId != GetUserId(user)) return Results.Forbid();

            var history = await firedRepo.GetRecentAsync(id, 50, ct);
            return Results.Ok(history);
        }).WithName("GetAlertHistory").WithTags("Alerts");

        group.MapPost("/test", (TestExpressionRequest request) =>
        {
            try
            {
                FilterExpressionParser.Parse(request.Expression);
                return Results.Ok(new { valid = true });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { valid = false, error = ex.Message });
            }
        }).WithName("TestExpression").WithTags("Alerts");

        return app;
    }

    private static string GetUserId(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name ?? "anonymous";

    private static void ValidateExpression(string expr)
    {
        try { FilterExpressionParser.Parse(expr); }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid filter expression: {ex.Message}", nameof(expr));
        }
    }

    private static AlertRule MapToRule(AlertRuleRequest r, string ownerId) => new()
    {
        Name = r.Name,
        OwnerId = ownerId,
        FilterExpression = r.FilterExpression,
        MinimumLevel = r.MinimumLevel,
        SourceApplicationFilter = r.SourceApplicationFilter,
        ThrottleMinutes = r.ThrottleMinutes,
        IsEnabled = r.IsEnabled,
        Recipients = r.RecipientEmails.Select(e => new AlertRecipient { Email = e }).ToList()
    };
}

public record AlertRuleRequest(
    string Name,
    string FilterExpression,
    Domain.Entities.LogLevel MinimumLevel,
    string? SourceApplicationFilter,
    int ThrottleMinutes,
    bool IsEnabled,
    List<string> RecipientEmails);

public record TestExpressionRequest(string Expression);
