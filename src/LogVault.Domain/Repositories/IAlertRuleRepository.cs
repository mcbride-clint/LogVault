using LogVault.Domain.Entities;

namespace LogVault.Domain.Repositories;

public interface IAlertRuleRepository
{
    Task<IReadOnlyList<AlertRule>> GetAllEnabledAsync(CancellationToken ct = default);
    Task<AlertRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AlertRule>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<AlertRule> UpsertAsync(AlertRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task UpdateLastFiredAsync(int id, DateTimeOffset firedAt, CancellationToken ct = default);
}
