using LogVault.Domain.Entities;

namespace LogVault.Domain.Repositories;

public interface IDashboardRepository
{
    Task<IReadOnlyList<Dashboard>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<Dashboard?> GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>Returns the owner's default dashboard, auto-creating a seeded one if none exists.</summary>
    Task<Dashboard> GetOrCreateDefaultAsync(string ownerId, CancellationToken ct = default);
    Task<Dashboard> CreateAsync(Dashboard dashboard, CancellationToken ct = default);
    Task<Dashboard> UpdateAsync(Dashboard dashboard, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<int> CountByOwnerAsync(string ownerId, CancellationToken ct = default);
}
