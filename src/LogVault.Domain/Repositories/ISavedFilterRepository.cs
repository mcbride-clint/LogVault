using LogVault.Domain.Entities;

namespace LogVault.Domain.Repositories;

public interface ISavedFilterRepository
{
    Task<IReadOnlyList<SavedFilter>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<SavedFilter?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SavedFilter> UpsertAsync(SavedFilter filter, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
