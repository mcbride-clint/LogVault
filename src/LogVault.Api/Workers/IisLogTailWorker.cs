using LogVault.Api.Configuration;
using LogVault.Application.Services;
using LogVault.Domain.Models;
using LogVault.Domain.Services;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace LogVault.Api.Workers;

public class IisLogTailWorker : BackgroundService
{
    private readonly IisWatcherOptions _options;
    private readonly IisWatcherRuntimeState _runtimeState;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IisLogTailWorker> _logger;

    private record FileState(long Offset, string[]? Fields, DateTime LastWriteUtc);
    private readonly ConcurrentDictionary<string, FileState> _fileStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (FileSystemWatcher Watcher, IisLogSource Source)> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public IisLogTailWorker(
        IOptions<IisWatcherOptions> options,
        IisWatcherRuntimeState runtimeState,
        IServiceScopeFactory scopeFactory,
        ILogger<IisLogTailWorker> logger)
    {
        _options = options.Value;
        _runtimeState = runtimeState;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _options.Sources.Length == 0)
        {
            _logger.LogInformation("IisLogTailWorker: disabled or no sources configured");
            return;
        }

        var pollingTasks = new List<Task>();

        foreach (var source in _options.Sources)
        {
            if (!Directory.Exists(source.Path))
            {
                _logger.LogWarning("IisLogTailWorker: directory not found, skipping: {Path}", source.Path);
                continue;
            }

            if (source.Path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                pollingTasks.Add(PollDirectoryAsync(source, stoppingToken));
            }
            else
            {
                var watcher = new FileSystemWatcher(source.Path, "*.log")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    InternalBufferSize = 65536,
                    EnableRaisingEvents = _runtimeState.IsEnabled(source.Path, source.Enabled)
                };

                watcher.Changed += (_, e) => OnFileChanged(e.FullPath, source, stoppingToken);
                watcher.Created += (_, e) => OnFileCreated(e.FullPath, source, stoppingToken);
                watcher.Error += (_, e) =>
                    _logger.LogWarning(e.GetException(), "IisLogTailWorker: FSW error on {Path}", source.Path);

                _watchers[source.Path] = (watcher, source);
                _logger.LogInformation("IisLogTailWorker: watching {Path} as {App}", source.Path, source.SourceApplication);
            }
        }

        // PeriodicTimer syncs FSW EnableRaisingEvents with runtime state within 1s of an admin toggle
        if (_watchers.Count > 0)
            pollingTasks.Add(SyncWatcherStateAsync(stoppingToken));

        try
        {
            await Task.WhenAll(
                Task.Delay(Timeout.Infinite, stoppingToken),
                Task.WhenAll(pollingTasks));
        }
        catch (OperationCanceledException) { }
        finally
        {
            foreach (var (watcher, _) in _watchers.Values)
                watcher.Dispose();
        }
    }

    private async Task SyncWatcherStateAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                foreach (var (_, (watcher, source)) in _watchers)
                {
                    var shouldBeEnabled = _runtimeState.IsEnabled(source.Path, source.Enabled);
                    if (watcher.EnableRaisingEvents != shouldBeEnabled)
                        watcher.EnableRaisingEvents = shouldBeEnabled;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnFileChanged(string fullPath, IisLogSource source, CancellationToken ct)
    {
        if (!_runtimeState.IsEnabled(source.Path, source.Enabled)) return;
        _ = ProcessFileAsync(fullPath, source, ct);
    }

    private void OnFileCreated(string fullPath, IisLogSource source, CancellationToken ct)
    {
        _fileStates.TryRemove(fullPath, out _);
        if (!_runtimeState.IsEnabled(source.Path, source.Enabled)) return;
        _ = ProcessFileAsync(fullPath, source, ct);
    }

    private async Task PollDirectoryAsync(IisLogSource source, CancellationToken ct)
    {
        var intervalMs = source.PollIntervalMs ?? _options.PollIntervalMs;
        _logger.LogInformation("IisLogTailWorker: polling {Path} every {Ms}ms as {App}",
            source.Path, intervalMs, source.SourceApplication);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                if (!_runtimeState.IsEnabled(source.Path, source.Enabled))
                    continue;

                foreach (var file in Directory.GetFiles(source.Path, "*.log"))
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        if (_fileStates.TryGetValue(file, out var state) && lastWrite <= state.LastWriteUtc)
                            continue;

                        await ProcessFileAsync(file, source, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "IisLogTailWorker: error checking {File}", file);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IisLogTailWorker: error polling {Path}", source.Path);
            }
        }
    }

    private async Task ProcessFileAsync(string fullPath, IisLogSource source, CancellationToken ct)
    {
        var lockSlim = _fileLocks.GetOrAdd(fullPath, _ => new SemaphoreSlim(1, 1));
        if (!await lockSlim.WaitAsync(0, ct))
            return;

        try
        {
            FileStream stream;
            try
            {
                stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException)
            {
                return;
            }

            await using (stream)
            {
                var lastWrite = File.GetLastWriteTimeUtc(fullPath);

                // First encounter: mark end-of-file as start position — no historical backfill
                if (!_fileStates.TryGetValue(fullPath, out var state))
                {
                    stream.Seek(0, SeekOrigin.End);
                    _fileStates[fullPath] = new FileState(stream.Position, null, lastWrite);
                    return;
                }

                // File was truncated/rotated between events
                if (state.Offset >= stream.Length)
                {
                    _fileStates[fullPath] = state with { LastWriteUtc = lastWrite };
                    return;
                }

                stream.Seek(state.Offset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var newContent = await reader.ReadToEndAsync(ct);
                var newOffset = stream.Position;

                if (string.IsNullOrEmpty(newContent))
                {
                    _fileStates[fullPath] = state with { LastWriteUtc = lastWrite };
                    return;
                }

                // Discover or reuse #Fields: column header
                var fields = state.Fields;
                if (fields == null)
                {
                    foreach (var line in newContent.Split('\n'))
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (trimmed.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase))
                        {
                            fields = trimmed[8..].Trim().Split(' ');
                            break;
                        }
                    }
                }

                if (fields == null)
                {
                    // Header not yet written by IIS; wait for next event
                    _fileStates[fullPath] = state with { LastWriteUtc = lastWrite };
                    return;
                }

                // Build parse input: first real read uses raw content (contains real #Fields: line);
                // subsequent reads prepend a synthetic header so IisLogParser gets a valid W3C stream.
                string parseContent = state.Fields == null
                    ? newContent
                    : "#Fields: " + string.Join(" ", fields) + "\n" + newContent;

                var streamBytes = Encoding.UTF8.GetBytes(parseContent);
                using var memStream = new MemoryStream(streamBytes);

                var context = new IngestContext(
                    XApplicationName: null,
                    XEnvironment: null,
                    TraceParentHeader: null,
                    ApiKeyDefaultApp: null,
                    ExplicitApplication: source.SourceApplication,
                    ExplicitEnvironment: source.SourceEnvironment);

                int count;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<ILogIngestionService>();
                    count = await svc.IngestFileAsync(memStream, LogFileFormat.IIS, context, ct);
                }

                _fileStates[fullPath] = new FileState(newOffset, fields, lastWrite);

                if (count > 0)
                    _logger.LogDebug("IisLogTailWorker: ingested {Count} events from {File}", count, fullPath);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IisLogTailWorker: error processing {File}", fullPath);
        }
        finally
        {
            lockSlim.Release();
        }
    }
}
