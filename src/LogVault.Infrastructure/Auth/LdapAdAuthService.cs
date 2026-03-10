using LogVault.Domain.Models;
using LogVault.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace LogVault.Infrastructure.Auth;

public class LdapAdAuthService : IAdAuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LdapAdAuthService> _logger;

    public LdapAdAuthService(IConfiguration config, ILogger<LdapAdAuthService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string Server => _config["ActiveDirectory:Server"] ?? "localhost";
    private int Port => int.TryParse(_config["ActiveDirectory:Port"], out var p) ? p : 389;
    private string SearchBase => _config["ActiveDirectory:SearchBase"] ?? "";
    private string Domain => _config["ActiveDirectory:Domain"] ?? "";

    public async Task<AdAuthResult> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            using var conn = new LdapConnection();
            await conn.ConnectAsync(Server, Port, ct);
            await conn.BindAsync($"{Domain}\\{username}", password, ct);

            var (displayName, email) = await GetUserAttributesAsync(conn, username, ct);
            return new AdAuthResult(true, displayName, email, null);
        }
        catch (LdapException ex)
        {
            _logger.LogWarning("LDAP authentication failed for {Username}: {Message}", username, ex.Message);
            return new AdAuthResult(false, null, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LDAP authentication for {Username}", username);
            return new AdAuthResult(false, null, null, "Authentication error");
        }
    }

    public async Task<IReadOnlyList<string>> GetGroupsAsync(string username, CancellationToken ct = default)
    {
        try
        {
            using var conn = new LdapConnection();
            await conn.ConnectAsync(Server, Port, ct);

            var groups = new List<string>();
            var searchFilter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(username)}))";
            var results = await conn.SearchAsync(SearchBase, LdapConnection.ScopeSub, searchFilter,
                ["memberOf"], false, ct);

            await foreach (var entry in results)
            {
                var memberOf = entry.GetOrDefault("memberOf", null);
                if (memberOf != null)
                {
                    foreach (var dn in memberOf.StringValueArray)
                    {
                        var cn = ExtractCn(dn);
                        if (cn != null) groups.Add(cn);
                    }
                }
            }

            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve groups for {Username}", username);
            return Array.Empty<string>();
        }
    }

    private async Task<(string? DisplayName, string? Email)> GetUserAttributesAsync(
        LdapConnection conn, string username, CancellationToken ct)
    {
        try
        {
            var searchFilter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(username)}))";
            var results = await conn.SearchAsync(SearchBase, LdapConnection.ScopeSub, searchFilter,
                ["displayName", "mail"], false, ct);

            await foreach (var entry in results)
            {
                var displayName = entry.GetOrDefault("displayName", null)?.StringValue;
                var email = entry.GetOrDefault("mail", null)?.StringValue;
                return (displayName, email);
            }
        }
        catch { /* non-critical */ }
        return (null, null);
    }

    private static string EscapeLdap(string input)
        => input.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29");

    private static string? ExtractCn(string dn)
    {
        var parts = dn.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return trimmed[3..];
        }
        return null;
    }
}
