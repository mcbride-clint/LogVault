using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Logs;

public class CorrelateModel(ILogEventRepository repo) : PageModel
{
    public string TraceId { get; private set; } = "";
    public IReadOnlyList<LogEvent> TraceEvents { get; private set; } = [];
    public IReadOnlyList<LogEvent> ContextBefore { get; private set; } = [];
    public IReadOnlyList<LogEvent> ContextAfter { get; private set; } = [];

    private static readonly string[] AppColors =
        ["#4e79a7","#f28e2b","#e15759","#76b7b2","#59a14f","#edc948","#b07aa1","#ff9da7","#9c755f","#bab0ac"];

    public string AppColor(string? app, IReadOnlyList<string> palette)
    {
        if (app is null) return "#aaa";
        var idx = palette.ToList().IndexOf(app);
        return idx >= 0 ? AppColors[idx % AppColors.Length] : "#aaa";
    }

    public IReadOnlyList<string> AppPalette { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string traceId, int contextMinutes = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(traceId)) return BadRequest();
        TraceId = traceId;

        var traceQuery = new LogEventQuery(
            From: null, To: null, MinLevel: null, MaxLevel: null,
            SourceApplication: null, SourceEnvironment: null,
            MessageContains: null, ExceptionContains: null,
            PropertyKey: null, PropertyValue: null, TraceId: traceId,
            Page: 1, PageSize: 500, SortBy: "Timestamp", Descending: false);
        var traceResult = await repo.QueryAsync(traceQuery, ct);
        TraceEvents = traceResult.Items;

        if (TraceEvents.Count == 0) return Page();

        var firstTs = TraceEvents.Min(e => e.Timestamp);
        var lastTs = TraceEvents.Max(e => e.Timestamp);
        var windowBefore = firstTs.AddMinutes(-Math.Max(1, contextMinutes));
        var windowAfter = lastTs.AddMinutes(Math.Max(1, contextMinutes));

        var apps = TraceEvents
            .Where(e => e.SourceApplication != null)
            .Select(e => e.SourceApplication!)
            .Distinct().ToList();
        AppPalette = apps;

        var before = new List<LogEvent>();
        var after = new List<LogEvent>();

        foreach (var appName in apps)
        {
            var bq = new LogEventQuery(
                From: windowBefore, To: firstTs, MinLevel: null, MaxLevel: null,
                SourceApplication: appName, SourceEnvironment: null,
                MessageContains: null, ExceptionContains: null,
                PropertyKey: null, PropertyValue: null, TraceId: null,
                Page: 1, PageSize: 100, SortBy: "Timestamp", Descending: true);
            var br = await repo.QueryAsync(bq, ct);
            before.AddRange(br.Items.Where(e => e.TraceId != traceId));

            var aq = new LogEventQuery(
                From: lastTs, To: windowAfter, MinLevel: null, MaxLevel: null,
                SourceApplication: appName, SourceEnvironment: null,
                MessageContains: null, ExceptionContains: null,
                PropertyKey: null, PropertyValue: null, TraceId: null,
                Page: 1, PageSize: 100, SortBy: "Timestamp", Descending: false);
            var ar = await repo.QueryAsync(aq, ct);
            after.AddRange(ar.Items.Where(e => e.TraceId != traceId));
        }

        ContextBefore = before.OrderByDescending(e => e.Timestamp).Take(100).ToList();
        ContextAfter = after.OrderBy(e => e.Timestamp).Take(100).ToList();
        return Page();
    }
}
