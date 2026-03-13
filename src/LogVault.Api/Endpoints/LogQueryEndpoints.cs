using LogVault.Application.Parsing;
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
            string? fts,
            string? propOp,
            string? expr,
            int page = 1,
            int pageSize = 50,
            string sort = "Timestamp",
            bool desc = true,
            ILogEventRepository repo = default!,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Min(pageSize, 500);

            // Parse expression and merge with individual params (expression takes precedence)
            ParsedQueryExpression? parsed = null;
            if (!string.IsNullOrWhiteSpace(expr))
            {
                parsed = LogQueryExpressionParser.Parse(expr);
                if (parsed.HasError)
                    return Results.BadRequest(new { error = parsed.Error });
            }

            var query = new LogEventQuery(
                From: parsed?.From ?? from,
                To: parsed?.To ?? to,
                MinLevel: parsed?.MinLevel ?? ParseLevel(level),
                MaxLevel: parsed?.MaxLevel ?? ParseLevel(maxLevel),
                SourceApplication: parsed?.SourceApplication ?? app,
                SourceEnvironment: parsed?.SourceEnvironment ?? env,
                MessageContains: parsed?.MessageContains ?? q,
                ExceptionContains: parsed?.ExceptionContains,
                PropertyKey: prop,
                PropertyValue: propValue,
                TraceId: parsed?.TraceId ?? traceId,
                Page: page, PageSize: pageSize,
                SortBy: sort, Descending: desc,
                FullTextSearch: fts,
                PropertyOp: Enum.TryParse<Domain.Models.PropertyFilterOp>(propOp, true, out var op)
                    ? op : Domain.Models.PropertyFilterOp.Contains,
                PropertyConditions: parsed?.PropertyConditions.Count > 0
                    ? parsed.PropertyConditions
                    : null);

            var result = await repo.QueryAsync(query, ct);
            return Results.Ok(result);
        }).WithName("QueryLogs").WithTags("Query");

        group.MapGet("/{id:long}", async (long id, ILogEventRepository repo, CancellationToken ct) =>
        {
            var ev = await repo.GetByIdAsync(id, ct);
            return ev is null ? Results.NotFound() : Results.Ok(ev);
        }).WithName("GetLogById").WithTags("Query");

        group.MapGet("/trace/{traceId}", async (
            string traceId,
            ILogEventRepository repo,
            CancellationToken ct) =>
        {
            var query = new LogEventQuery(
                From: null, To: null,
                MinLevel: null, MaxLevel: null,
                SourceApplication: null, SourceEnvironment: null,
                MessageContains: null, ExceptionContains: null,
                PropertyKey: null, PropertyValue: null,
                TraceId: traceId,
                Page: 1, PageSize: 500,
                SortBy: "Timestamp", Descending: false);

            var result = await repo.QueryAsync(query, ct);
            return Results.Ok(result.Items);
        }).WithName("GetTrace").WithTags("Query");

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

        group.MapGet("/stats/top-applications", async (
            ILogEventRepository repo,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int limit = 10,
            CancellationToken ct = default) =>
        {
            var f = from ?? DateTimeOffset.UtcNow.AddHours(-24);
            var t = to ?? DateTimeOffset.UtcNow;
            var results = await repo.GetTopApplicationsAsync(f, t, Math.Min(limit, 50), ct);
            return Results.Ok(results);
        }).WithName("GetTopApplications").WithTags("Query");

        group.MapGet("/correlate", async (
            string? traceId,
            int contextMinutes = 5,
            ILogEventRepository repo = default!,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrEmpty(traceId))
                return Results.BadRequest(new { error = "traceId is required" });

            var traceQuery = new LogEventQuery(
                From: null, To: null, MinLevel: null, MaxLevel: null,
                SourceApplication: null, SourceEnvironment: null,
                MessageContains: null, ExceptionContains: null,
                PropertyKey: null, PropertyValue: null, TraceId: traceId,
                Page: 1, PageSize: 500, SortBy: "Timestamp", Descending: false);
            var traceResult = await repo.QueryAsync(traceQuery, ct);
            var traceEvents = traceResult.Items;

            if (traceEvents.Count == 0)
                return Results.Ok(new { traceId, traceEvents, contextBefore = new List<object>(), contextAfter = new List<object>() });

            var firstTs = traceEvents.Min(e => e.Timestamp);
            var lastTs = traceEvents.Max(e => e.Timestamp);
            var windowBefore = firstTs.AddMinutes(-Math.Max(1, contextMinutes));
            var windowAfter = lastTs.AddMinutes(Math.Max(1, contextMinutes));

            var apps = traceEvents
                .Where(e => e.SourceApplication != null)
                .Select(e => e.SourceApplication!)
                .Distinct()
                .ToList();

            var contextBefore = new List<Domain.Entities.LogEvent>();
            var contextAfter = new List<Domain.Entities.LogEvent>();

            foreach (var appName in apps)
            {
                var beforeQuery = new LogEventQuery(
                    From: windowBefore, To: firstTs,
                    MinLevel: null, MaxLevel: null,
                    SourceApplication: appName, SourceEnvironment: null,
                    MessageContains: null, ExceptionContains: null,
                    PropertyKey: null, PropertyValue: null, TraceId: null,
                    Page: 1, PageSize: 100, SortBy: "Timestamp", Descending: true);
                var beforeResult = await repo.QueryAsync(beforeQuery, ct);
                contextBefore.AddRange(beforeResult.Items.Where(e => e.TraceId != traceId));

                var afterQuery = new LogEventQuery(
                    From: lastTs, To: windowAfter,
                    MinLevel: null, MaxLevel: null,
                    SourceApplication: appName, SourceEnvironment: null,
                    MessageContains: null, ExceptionContains: null,
                    PropertyKey: null, PropertyValue: null, TraceId: null,
                    Page: 1, PageSize: 100, SortBy: "Timestamp", Descending: false);
                var afterResult = await repo.QueryAsync(afterQuery, ct);
                contextAfter.AddRange(afterResult.Items.Where(e => e.TraceId != traceId));
            }

            var sortedBefore = contextBefore.OrderByDescending(e => e.Timestamp).Take(100).ToList();
            var sortedAfter = contextAfter.OrderBy(e => e.Timestamp).Take(100).ToList();

            return Results.Ok(new { traceId, traceEvents, contextBefore = sortedBefore, contextAfter = sortedAfter });
        }).WithName("GetCorrelation").WithTags("Query");

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
