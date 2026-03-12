using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfApiKeyRepository : IApiKeyRepository
{
    private readonly LogVaultDbContext _db;

    public EfApiKeyRepository(LogVaultDbContext db) => _db = db;

    public async Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken ct = default)
        => await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

    public async Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct = default)
        => await _db.ApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync(ct);

    public async Task<ApiKey> CreateAsync(ApiKey key, CancellationToken ct = default)
    {
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(ct);
        return key;
    }

    public async Task RevokeAsync(int id, CancellationToken ct = default)
    {
        await _db.ApiKeys
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(k => k.IsRevoked, true)
                .SetProperty(k => k.IsEnabled, false), ct);
    }

    public async Task<(ApiKey NewKey, ApiKey ExpiredOldKey)> RotateAsync(
        int existingKeyId, ApiKey newKey, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var old = await _db.ApiKeys.FindAsync([existingKeyId], ct)
                ?? throw new KeyNotFoundException($"ApiKey {existingKeyId} not found");

            // Carry the original label to the new key; rename old key with recycled date
            newKey.Label = old.Label;
            newKey.DefaultApplication = old.DefaultApplication;
            old.Label = $"{old.Label} (recycled {newKey.CreatedAt:yyyy-MM-dd})";
            old.ExpiresAt = newKey.CreatedAt.AddHours(24);

            newKey.RotatedFromId = existingKeyId;
            _db.ApiKeys.Add(newKey);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return (newKey, old);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.ApiKeys
            .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < now)
            .ExecuteDeleteAsync(ct);
    }
}
