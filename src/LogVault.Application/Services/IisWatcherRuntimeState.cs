using System.Collections.Concurrent;

namespace LogVault.Application.Services;

public class IisWatcherRuntimeState
{
    private readonly ConcurrentDictionary<string, bool> _overrides =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string path, bool configDefault) =>
        _overrides.TryGetValue(path, out var v) ? v : configDefault;

    public void SetEnabled(string path, bool enabled) =>
        _overrides[path] = enabled;
}
