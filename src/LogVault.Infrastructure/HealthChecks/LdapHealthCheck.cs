using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Novell.Directory.Ldap;

namespace LogVault.Infrastructure.HealthChecks;

public class LdapHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;

    public LdapHealthCheck(IConfiguration config) => _config = config;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var host = _config["ActiveDirectory:Server"];
        if (string.IsNullOrEmpty(host))
            return HealthCheckResult.Degraded("LDAP server not configured");

        var port = int.TryParse(_config["ActiveDirectory:Port"], out var p) ? p : 389;
        var timeout = int.TryParse(_config["HealthChecks:LdapProbeTimeoutSeconds"], out var t) ? t : 5;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var conn = new LdapConnection();
            await conn.ConnectAsync(host, port, cts.Token);
            conn.Disconnect();

            return HealthCheckResult.Healthy($"LDAP {host}:{port} reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"LDAP {host}:{port} unreachable: {ex.Message}");
        }
    }
}
