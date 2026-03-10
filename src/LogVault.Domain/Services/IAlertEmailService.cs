using LogVault.Domain.Entities;

namespace LogVault.Domain.Services;

public interface IAlertEmailService
{
    Task SendAlertAsync(AlertRule rule, LogEvent triggeringEvent, CancellationToken ct = default);
}
