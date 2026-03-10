using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using LogVault.Domain.Services;

namespace LogVault.Api.Endpoints;

public static class LogQueryEndpoints
{
    public static IEndpointRouteBuilder MapLogQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs").RequireAuthorization("RequireUser");

        group.MapGet("", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? level,
            string? maxLevel,
            string? app,
            string? env,
            string? q,
            string? prop,
            string? propValue,
            string? traceId,
            int page = 1,
            int pageSize = 50,
            string sort = "Timestamp",
            bool desc = true,
            ILogEventRepository repo = default!,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Min(pageSize, 500);
            var query = new LogEventQuery(
                From: from, To: to,
                MinLevel: ParseLevel(level), MaxLevel: ParseLevel(maxLevel),
                SourceApplication: app, SourceEnvironment: env,
                MessageContains: q, ExceptionContains: null,
                PropertyKey: prop, PropertyValue: propValue,
                TraceId: traceId,
                Page: page, PageSize: pageSize,
                SortBy: sort, Descending: desc);

            var result = await repo.QueryAsync(query, ct);
            return Results.Ok(result);
        }).WithName("QueryLogs").WithTags("Query");

        group.MapGet("/{id:long}", async (long id, ILogEventRepository repo, CancellationToken ct) =>
        {
            var ev = await repo.GetByIdAsync(id, ct);
            return ev is null ? Results.NotFound() : Results.Ok(ev);
        }).WithName("GetLogById").WithTags("Query");

        group.MapGet("/stats", async (
            ILogEventRepository repo,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken ct) =>
        {
            var f = from ?? DateTimeOffset.UtcNow.AddHours(-24);
            var t = to ?? DateTimeOffset.UtcNow;
            var stats = await repo.GetStatsAsync(f, t, ct);
            return Results.Ok(stats);
        }).WithName("GetStats").WithTags("Query");

        group.MapGet("/export", async (
            HttpContext ctx,
            string? format,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? level,
            string? appFilter,
            string? q,
            string? traceId,
            ILogExportService export,
            CancellationToken ct) =>
        {
            var fmt = format?.ToLowerInvariant() ?? "json";
            var query = new LogEventQuery(
                From: from, To: to,
                MinLevel: ParseLevel(level), MaxLevel: null,
                SourceApplication: appFilter, SourceEnvironment: null,
                MessageContains: q, ExceptionContains: null,
                PropertyKey: null, PropertyValue: null,
                TraceId: traceId,
                Page: 1, PageSize: int.MaxValue,
                SortBy: "Timestamp", Descending: true);

            var filename = $"logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{fmt}";
            var contentType = fmt == "csv" ? "text/csv" : "application/json";

            return Results.Stream(
                stream => fmt == "csv"
                    ? export.ExportCsvAsync(query, stream, ct)
                    : export.ExportJsonAsync(query, stream, ct),
                contentType: contentType,
                fileDownloadName: filename);
        }).WithName("ExportLogs").WithTags("Query");

        return app;
    }

    private static Domain.Entities.LogLevel? ParseLevel(string? level)
    {
        if (string.IsNullOrEmpty(level)) return null;
        return Enum.TryParse<Domain.Entities.LogLevel>(level, true, out var l) ? l : null;
    }
}
