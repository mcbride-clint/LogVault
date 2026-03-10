using LogVault.Domain.Entities;
using LogVault.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace LogVault.Api.Hubs;

public class LogHubNotifier : ILogHubNotifier
{
    private readonly IHubContext<LogHub> _hubContext;
    private readonly ConcurrentDictionary<string, LogHubFilter> _filters = new();
    private readonly int _maxEventsPerBroadcast;

    public LogHubNotifier(IHubContext<LogHub> hubContext, IConfiguration config)
    {
        _hubContext = hubContext;
        _maxEventsPerBroadcast = int.TryParse(config["SignalR:LiveTailMaxEventsPerBroadcast"], out var m) ? m : 100;
    }

    public void RegisterFilter(string connectionId, LogHubFilter filter)
        => _filters[connectionId] = filter;

    public void UnregisterFilter(string connectionId)
        => _filters.TryRemove(connectionId, out _);

    public async Task BroadcastAsync(IReadOnlyList<LogEvent> newEvents, CancellationToken ct = default)
    {
        if (_filters.IsEmpty || newEvents.Count == 0) return;

        foreach (var (connectionId, filter) in _filters)
        {
            var matching = newEvents
                .Where(e => Matches(e, filter))
                .Take(_maxEventsPerBroadcast)
                .Select(e => new LogEventDto(e))
                .ToList();

            if (matching.Count > 0)
            {
                try
                {
                    await _hubContext.Clients.Client(connectionId)
                        .SendAsync("NewEvents", matching, ct);
                }
                catch { /* client may have disconnected */ }
            }
        }
    }

    private static bool Matches(LogEvent ev, LogHubFilter filter)
    {
        if (filter.MinLevel.HasValue && ev.Level < filter.MinLevel.Value) return false;
        if (!string.IsNullOrEmpty(filter.SourceApplication) &&
            !string.Equals(ev.SourceApplication, filter.SourceApplication, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(filter.SourceEnvironment) &&
            !string.Equals(ev.SourceEnvironment, filter.SourceEnvironment, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(filter.MessageContains) &&
            !ev.RenderedMessage.Contains(filter.MessageContains, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}

public record LogEventDto(
    long Id,
    DateTimeOffset Timestamp,
    string Level,
    string? SourceApplication,
    string RenderedMessage,
    bool HasException,
    string? TraceId)
{
    public LogEventDto(LogEvent ev) : this(
        ev.Id, ev.Timestamp, ev.Level.ToString(), ev.SourceApplication,
        ev.RenderedMessage, !string.IsNullOrEmpty(ev.Exception), ev.TraceId) { }
}
