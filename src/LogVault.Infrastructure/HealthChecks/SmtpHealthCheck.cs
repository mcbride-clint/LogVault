using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogVault.Infrastructure.HealthChecks;

public class SmtpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;

    public SmtpHealthCheck(IConfiguration config) => _config = config;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var host = _config["Email:SmtpHost"];
        if (string.IsNullOrEmpty(host))
            return HealthCheckResult.Degraded("SMTP host not configured");

        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 25;
        var timeout = int.TryParse(_config["HealthChecks:SmtpProbeTimeoutSeconds"], out var t) ? t : 5;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.None, cts.Token);
            await client.DisconnectAsync(true, cts.Token);

            return HealthCheckResult.Healthy($"SMTP {host}:{port} reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"SMTP {host}:{port} unreachable: {ex.Message}");
        }
    }
}
