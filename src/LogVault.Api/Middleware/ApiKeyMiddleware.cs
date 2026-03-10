using LogVault.Domain.Repositories;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LogVault.Api.Middleware;

public class ApiKeyMiddleware : IMiddleware
{
    private readonly IApiKeyRepository _apiKeyRepo;

    public ApiKeyMiddleware(IApiKeyRepository apiKeyRepo) => _apiKeyRepo = apiKeyRepo;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var rawKey))
        {
            var hash = ComputeSha256(rawKey.ToString());
            var key = await _apiKeyRepo.FindByHashAsync(hash, context.RequestAborted);

            if (key != null && key.IsUsable)
            {
                context.Items["ApiKeyDefaultApp"] = key.DefaultApplication;
                context.Items["ApiKeyId"] = key.Id;

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, $"ApiKey:{key.Label}"),
                    new Claim(ClaimTypes.Role, "Ingest"),
                    new Claim("auth_method", "ApiKey")
                };
                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await next(context);
    }

    public static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
