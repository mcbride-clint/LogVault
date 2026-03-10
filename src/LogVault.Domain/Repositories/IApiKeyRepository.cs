using LogVault.Domain.Entities;

namespace LogVault.Domain.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct = default);
    Task<ApiKey> CreateAsync(ApiKey key, CancellationToken ct = default);
    Task RevokeAsync(int id, CancellationToken ct = default);
    Task<(ApiKey NewKey, ApiKey ExpiredOldKey)> RotateAsync(int existingKeyId, ApiKey newKey, CancellationToken ct = default);
    Task PurgeExpiredAsync(CancellationToken ct = default);
}
