using LogVault.Domain.Entities;

namespace LogVault.Domain.Repositories;

public interface IAlertFiredRepository
{
    Task RecordAsync(AlertFired fired, CancellationToken ct = default);
    Task<IReadOnlyList<AlertFired>> GetRecentAsync(int alertRuleId, int count, CancellationToken ct = default);
}
