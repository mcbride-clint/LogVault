using LogVault.Domain.Entities;

namespace LogVault.Domain.Services;

public interface IWebhookNotificationService
{
    Task SendAsync(AlertRule rule, LogEvent triggeringEvent, CancellationToken ct = default);
}
