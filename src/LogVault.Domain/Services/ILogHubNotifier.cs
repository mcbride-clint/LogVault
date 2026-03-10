using LogVault.Domain.Entities;

namespace LogVault.Domain.Services;

public interface ILogHubNotifier
{
    Task BroadcastAsync(IReadOnlyList<LogEvent> newEvents, CancellationToken ct = default);
    void RegisterFilter(string connectionId, LogHubFilter filter);
    void UnregisterFilter(string connectionId);
}

public class LogHubFilter
{
    public Domain.Entities.LogLevel? MinLevel { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceEnvironment { get; set; }
    public string? MessageContains { get; set; }
}
