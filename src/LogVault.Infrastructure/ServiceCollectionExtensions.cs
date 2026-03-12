using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using LogVault.Infrastructure.HealthChecks;
using LogVault.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogVault.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogVaultInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var provider = config["DatabaseProvider"] ?? "Sqlite";
        var connectionString = config.GetConnectionString("Default") ?? "Data Source=logvault.db";

        if (provider.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
        {
            // Oracle registration kept for future use
            throw new NotSupportedException("Oracle provider is not configured. Use Sqlite.");
        }
        else
        {
            services.AddDbContext<LogVaultDbContext>(o => o.UseSqlite(connectionString));
        }

        services.AddScoped<ILogEventRepository, EfLogEventRepository>();
        services.AddScoped<IAlertRuleRepository, EfAlertRuleRepository>();
        services.AddScoped<IAlertFiredRepository, EfAlertFiredRepository>();
        services.AddScoped<IApiKeyRepository, EfApiKeyRepository>();
        services.AddScoped<ISavedFilterRepository, EfSavedFilterRepository>();

        services.AddTransient<SmtpHealthCheck>();

        return services;
    }
}
