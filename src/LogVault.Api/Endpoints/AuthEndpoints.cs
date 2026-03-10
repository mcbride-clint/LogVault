using LogVault.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace LogVault.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            HttpContext ctx,
            IAdAuthService adAuth,
            Microsoft.Extensions.Configuration.IConfiguration config,
            CancellationToken ct) =>
        {
            var result = await adAuth.AuthenticateAsync(request.Username, request.Password, ct);
            if (!result.Success)
                return Results.Unauthorized();

            var groups = await adAuth.GetGroupsAsync(request.Username, ct);
            var adminGroup = config["ActiveDirectory:AdminGroup"] ?? "LogVault-Admins";
            var userGroup = config["ActiveDirectory:UserGroup"] ?? "LogVault-Users";

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, request.Username),
                new("DisplayName", result.DisplayName ?? request.Username),
            };

            if (result.Email != null) claims.Add(new(ClaimTypes.Email, result.Email));

            if (groups.Contains(adminGroup, StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new(ClaimTypes.Role, "Admin"));
                claims.Add(new(ClaimTypes.Role, "User"));
            }
            else if (groups.Contains(userGroup, StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new(ClaimTypes.Role, "User"));
            }
            else
            {
                return Results.Forbid();
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = request.RememberMe });

            return Results.Ok(new
            {
                Username = request.Username,
                DisplayName = result.DisplayName,
                Email = result.Email,
                Roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
            });
        }).WithName("Login").WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        }).WithName("Logout").WithTags("Auth");

        app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
        {
            if (user.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                Username = user.FindFirst(ClaimTypes.Name)?.Value,
                DisplayName = user.FindFirst("DisplayName")?.Value,
                Email = user.FindFirst(ClaimTypes.Email)?.Value,
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
                AuthMethod = user.FindFirst("auth_method")?.Value ?? "Cookie"
            });
        }).WithName("GetCurrentUser").WithTags("Auth").RequireAuthorization();

        return app;
    }
}

public record LoginRequest(string Username, string Password, bool RememberMe = false);
