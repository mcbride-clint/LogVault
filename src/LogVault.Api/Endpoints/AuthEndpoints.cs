using System.Security.Claims;
using System.Security.Principal;

namespace LogVault.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", (ClaimsPrincipal user, IWebHostEnvironment env) =>
        {
            if (user.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity.Name;

            // For Windows identities, strip domain prefix for display (e.g. "DOMAIN\user" → "user")
            var displayName = user.FindFirst("DisplayName")?.Value
                ?? (username?.Contains('\\') == true
                    ? username[(username.LastIndexOf('\\') + 1)..]
                    : username);

            var authMethod = user.FindFirst("auth_method")?.Value
                ?? (user.Identity is WindowsIdentity ? "Windows" : "Unknown");

            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            object response = env.IsDevelopment()
                ? new
                {
                    Username = username,
                    DisplayName = displayName,
                    Email = user.FindFirst(ClaimTypes.Email)?.Value,
                    Roles = roles,
                    AuthMethod = authMethod,
                    // Dev-only diagnostics
                    _Debug = new
                    {
                        IdentityType = user.Identity?.GetType().Name,
                        AllClaims = user.Claims.Select(c => new { c.Type, c.Value }).ToList()
                    }
                }
                : (object)new
                {
                    Username = username,
                    DisplayName = displayName,
                    Email = user.FindFirst(ClaimTypes.Email)?.Value,
                    Roles = roles,
                    AuthMethod = authMethod
                };

            return Results.Ok(response);
        }).WithName("GetCurrentUser").WithTags("Auth").RequireAuthorization();

        return app;
    }
}
