using LogVault.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogVault.Application.Workers;

public class RetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RetentionWorker> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<RetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRetentionAsync(stoppingToken);

            var delay = TimeUntilNextMidnightUtc();
            _logger.LogDebug("Next retention run in {Delay}", delay);
            await Task.Delay(delay, stoppingToken).ContinueWith(_ => { });
        }
    }

    public async Task<int> RunRetentionAsync(CancellationToken ct = default)
    {
        var autoEnabled = bool.TryParse(_config["Retention:AutoPurgeEnabled"], out var ae) && ae;
        if (!autoEnabled) return 0;

        if (!await _semaphore.WaitAsync(TimeSpan.Zero, ct)) return 0;
        try
        {
            var retainDays = int.TryParse(_config["Retention:RetainDays"], out var rd) ? rd : 90;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retainDays);

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILogEventRepository>();
            var deleted = await repo.DeleteOlderThanAsync(cutoff, ct);

            _logger.LogInformation("Retention run: deleted {Count} events older than {Cutoff:u}", deleted, cutoff);
            return deleted;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static TimeSpan TimeUntilNextMidnightUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var midnight = now.Date.AddDays(1);
        return midnight - now.DateTime;
    }
}
