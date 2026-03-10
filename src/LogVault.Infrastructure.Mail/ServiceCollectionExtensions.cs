using LogVault.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogVault.Infrastructure.Mail;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogVaultMail(this IServiceCollection services)
    {
        services.AddScoped<IAlertEmailService, MailKitAlertEmailService>();
        return services;
    }
}
