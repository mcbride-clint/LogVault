using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfSavedFilterRepository : ISavedFilterRepository
{
    private readonly LogVaultDbContext _db;

    public EfSavedFilterRepository(LogVaultDbContext db) => _db = db;

    public async Task<IReadOnlyList<SavedFilter>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await _db.SavedFilters.Where(f => f.OwnerId == ownerId).OrderBy(f => f.Name).ToListAsync(ct);

    public async Task<SavedFilter?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.SavedFilters.FindAsync([id], ct);

    public async Task<SavedFilter> UpsertAsync(SavedFilter filter, CancellationToken ct = default)
    {
        if (filter.Id == 0)
        {
            filter.CreatedAt = DateTimeOffset.UtcNow;
            filter.UpdatedAt = filter.CreatedAt;
            _db.SavedFilters.Add(filter);
        }
        else
        {
            filter.UpdatedAt = DateTimeOffset.UtcNow;
            _db.SavedFilters.Update(filter);
        }

        await _db.SaveChangesAsync(ct);
        return filter;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _db.SavedFilters.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
    }
}
