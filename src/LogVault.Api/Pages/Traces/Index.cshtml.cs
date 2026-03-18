using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Traces;

public class IndexModel(ILogEventRepository repo) : PageModel
{
    public string TraceId { get; private set; } = "";
    public IReadOnlyList<SpanGroup> Spans { get; private set; } = [];
    public DateTimeOffset TraceStart { get; private set; }
    public TimeSpan TraceDuration { get; private set; }

    public record SpanGroup(string? SpanId, IReadOnlyList<LogEvent> Events, DateTimeOffset Start, DateTimeOffset End);

    public async Task<IActionResult> OnGetAsync(string traceId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(traceId)) return BadRequest();
        TraceId = traceId;

        var query = new LogEventQuery(
            From: null, To: null, MinLevel: null, MaxLevel: null,
            SourceApplication: null, SourceEnvironment: null,
            MessageContains: null, ExceptionContains: null,
            PropertyKey: null, PropertyValue: null, TraceId: traceId,
            Page: 1, PageSize: 500, SortBy: "Timestamp", Descending: false);

        var result = await repo.QueryAsync(query, ct);
        var events = result.Items;

        if (events.Count > 0)
        {
            TraceStart = events.Min(e => e.Timestamp);
            var traceEnd = events.Max(e => e.Timestamp);
            TraceDuration = traceEnd - TraceStart;

            Spans = events
                .GroupBy(e => e.SpanId)
                .Select(g => new SpanGroup(g.Key, g.OrderBy(e => e.Timestamp).ToList(),
                    g.Min(e => e.Timestamp), g.Max(e => e.Timestamp)))
                .OrderBy(s => s.Start)
                .ToList();
        }

        return Page();
    }

    public double OffsetPercent(DateTimeOffset ts) =>
        TraceDuration.TotalMilliseconds > 0
            ? (ts - TraceStart).TotalMilliseconds / TraceDuration.TotalMilliseconds * 100
            : 0;

    public double WidthPercent(DateTimeOffset start, DateTimeOffset end) =>
        TraceDuration.TotalMilliseconds > 0
            ? Math.Max(0.5, (end - start).TotalMilliseconds / TraceDuration.TotalMilliseconds * 100)
            : 0.5;
}
