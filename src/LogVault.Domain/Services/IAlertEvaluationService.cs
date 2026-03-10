using LogVault.Domain.Entities;

namespace LogVault.Domain.Services;

public interface IAlertEvaluationService
{
    Task EvaluateAsync(IReadOnlyList<LogEvent> newEvents, CancellationToken ct = default);
}
