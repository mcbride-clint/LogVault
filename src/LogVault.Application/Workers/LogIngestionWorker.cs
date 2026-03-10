using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace LogVault.Application.Workers;

public class LogIngestionWorker : BackgroundService
{
    private readonly Channel<IReadOnlyList<LogEvent>> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogHubNotifier _hubNotifier;
    private readonly ILogger<LogIngestionWorker> _logger;
    private readonly int _maxBatchSize;
    private readonly int _flushIntervalMs;

    public LogIngestionWorker(
        Channel<IReadOnlyList<LogEvent>> channel,
        IServiceScopeFactory scopeFactory,
        ILogHubNotifier hubNotifier,
        ILogger<LogIngestionWorker> logger,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _hubNotifier = hubNotifier;
        _logger = logger;
        _maxBatchSize = int.TryParse(config["Ingestion:MaxBatchSize"], out var bs) ? bs : 500;
        _flushIntervalMs = int.TryParse(config["Ingestion:FlushIntervalMs"], out var fi) ? fi : 500;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogIngestionWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = new List<LogEvent>();
                var deadline = DateTimeOffset.UtcNow.AddMilliseconds(_flushIntervalMs);

                while (batch.Count < _maxBatchSize && DateTimeOffset.UtcNow < deadline)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var remaining = deadline - DateTimeOffset.UtcNow;
                    if (remaining <= TimeSpan.Zero) break;
                    cts.CancelAfter(remaining);

                    try
                    {
                        if (await _channel.Reader.WaitToReadAsync(cts.Token))
                        {
                            while (_channel.Reader.TryRead(out var events) && batch.Count < _maxBatchSize)
                                batch.AddRange(events);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                }

                if (batch.Count > 0)
                    await CommitBatchAsync(batch, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error in ingestion worker loop");
                await Task.Delay(1000, stoppingToken).ContinueWith(_ => { });
            }
        }

        _logger.LogInformation("LogIngestionWorker stopped");
    }

    private async Task CommitBatchAsync(List<LogEvent> batch, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILogEventRepository>();
            await repo.BulkInsertAsync(batch, ct);

            var alertSvc = scope.ServiceProvider.GetRequiredService<IAlertEvaluationService>();
            await alertSvc.EvaluateAsync(batch, ct);

            await _hubNotifier.BroadcastAsync(batch, ct);

            _logger.LogDebug("Committed {Count} log events", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit batch of {Count} events", batch.Count);
        }
    }
}
