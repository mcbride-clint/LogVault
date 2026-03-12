using LogVault.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;

namespace LogVault.Api.Auth;

/// <summary>
/// Maps Windows AD group membership to application roles (Admin / User)
/// by translating the SIDs on the WindowsIdentity to group names.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsRolesClaimsTransformation : IClaimsTransformation
{
    private readonly ActiveDirectoryOptions _options;
    private readonly ILogger<WindowsRolesClaimsTransformation> _logger;

    public WindowsRolesClaimsTransformation(
        IOptions<ActiveDirectoryOptions> options,
        ILogger<WindowsRolesClaimsTransformation> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identityType = principal.Identity?.GetType().Name ?? "null";
        var isAuthenticated = principal.Identity?.IsAuthenticated == true;
        var hasRoles = principal.HasClaim(c => c.Type == ClaimTypes.Role);

        _logger.LogDebug(
            "ClaimsTransformation: IdentityType={IdentityType}, IsAuthenticated={IsAuthenticated}, HasRoles={HasRoles}, GrantAll={GrantAll}",
            identityType, isAuthenticated, hasRoles, _options.GrantAllAuthenticatedUsers);

        if (!isAuthenticated)
            return Task.FromResult(principal);

        if (hasRoles)
            return Task.FromResult(principal);

        List<Claim>? roleClaims;

        if (_options.GrantAllAuthenticatedUsers)
        {
            // Dev bypass: grant Admin+User to any authenticated user regardless of identity type
            roleClaims = [new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.Role, "User")];
        }
        else
        {
            // Production: map Windows AD group membership to roles
            if (principal.Identity is not WindowsIdentity wi)
                return Task.FromResult(principal);

            var groupNames = GetWindowsGroupNames(wi);

            if (groupNames.Any(g => g.Equals(_options.AdminGroup, StringComparison.OrdinalIgnoreCase)))
                roleClaims = [new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.Role, "User")];
            else if (groupNames.Any(g => g.Equals(_options.UserGroup, StringComparison.OrdinalIgnoreCase)))
                roleClaims = [new Claim(ClaimTypes.Role, "User")];
            else
                return Task.FromResult(principal); // not in any LogVault group → no roles → 403
        }

        var cloned = principal.Clone();
        // Add a separate plain ClaimsIdentity so role checks go through ClaimsIdentity.IsInRole()
        // rather than WindowsIdentity.IsInRole(), which uses the Windows security token.
        cloned.AddIdentity(new ClaimsIdentity(roleClaims));
        return Task.FromResult(cloned);
    }

    private static List<string> GetWindowsGroupNames(WindowsIdentity wi)
    {
        var names = new List<string>();
        if (wi.Groups == null) return names;

        foreach (var sid in wi.Groups)
        {
            try
            {
                var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));
                var fullName = ntAccount.Value; // e.g. "DOMAIN\GroupName"
                var slash = fullName.LastIndexOf('\\');
                names.Add(slash >= 0 ? fullName[(slash + 1)..] : fullName);
            }
            catch
            {
                // SID may not be resolvable in some environments — skip
            }
        }

        return names;
    }
}
