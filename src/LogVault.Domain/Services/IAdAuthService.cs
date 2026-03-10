using LogVault.Domain.Models;

namespace LogVault.Domain.Services;

public interface IAdAuthService
{
    Task<AdAuthResult> AuthenticateAsync(string username, string password, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetGroupsAsync(string username, CancellationToken ct = default);
}
