using LogVault.Application.Alerts;
using LogVault.Application.Services;
using LogVault.Application.Workers;
using LogVault.Domain.Entities;
using LogVault.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace LogVault.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogVaultApplication(
        this IServiceCollection services, IConfiguration config)
    {
        var channelCapacity = int.TryParse(config["Ingestion:ChannelCapacity"], out var cap) ? cap : 1000;

        services.AddSingleton(Channel.CreateBounded<IReadOnlyList<LogEvent>>(
            new BoundedChannelOptions(channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            }));

        services.AddScoped<ILogIngestionService, LogIngestionService>();
        services.AddScoped<ISourceApplicationResolver, SourceApplicationResolver>();
        services.AddScoped<ILogExportService, LogExportService>();
        services.AddScoped<IAlertEvaluationService, AlertEvaluationService>();

        services.AddSingleton<AlertRuleExpressionCache>();

        services.AddHostedService<LogIngestionWorker>();
        services.AddSingleton<RetentionWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<RetentionWorker>());

        return services;
    }
}
