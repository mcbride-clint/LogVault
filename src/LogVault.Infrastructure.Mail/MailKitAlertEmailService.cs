using LogVault.Domain.Entities;
using LogVault.Domain.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Text;
using System.Text.Json;

namespace LogVault.Infrastructure.Mail;

public class MailKitAlertEmailService : IAlertEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<MailKitAlertEmailService> _logger;

    public MailKitAlertEmailService(IConfiguration config, ILogger<MailKitAlertEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAlertAsync(AlertRule rule, LogEvent triggeringEvent, CancellationToken ct = default)
    {
        if (!rule.Recipients.Any())
        {
            _logger.LogWarning("AlertRule {RuleId} has no recipients", rule.Id);
            return;
        }

        var message = BuildMessage(rule, triggeringEvent);

        var host = _config["Email:SmtpHost"] ?? "localhost";
        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 25;
        var useSsl = bool.TryParse(_config["Email:UseSsl"], out var ssl) && ssl;

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port,
                useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect
                        : MailKit.Security.SecureSocketOptions.None, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Alert email sent for rule {RuleName} to {Count} recipient(s)",
                rule.Name, rule.Recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert email for rule {RuleId}", rule.Id);
            throw;
        }
    }

    private MimeMessage BuildMessage(AlertRule rule, LogEvent ev)
    {
        var fromAddress = _config["Email:FromAddress"] ?? "logvault@localhost";
        var fromName = _config["Email:FromName"] ?? "LogVault Alerts";

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, fromAddress));
        foreach (var recipient in rule.Recipients)
            msg.To.Add(MailboxAddress.Parse(recipient.Email));

        msg.Subject = $"[LogVault Alert] {rule.Name} — {ev.Level} in {ev.SourceApplication ?? "Unknown"}";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = BuildHtmlBody(rule, ev),
            TextBody = BuildTextBody(rule, ev)
        };
        msg.Body = bodyBuilder.ToMessageBody();
        return msg;
    }

    private static string BuildHtmlBody(AlertRule rule, LogEvent ev)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><body style=\"font-family:sans-serif;\">");
        sb.AppendLine($"<h2 style=\"color:#c0392b;\">LogVault Alert: {HtmlEncode(rule.Name)}</h2>");
        sb.AppendLine("<table style=\"border-collapse:collapse;width:100%;\">");
        AddRow(sb, "Rule", rule.Name);
        AddRow(sb, "Time", ev.Timestamp.ToString("u"));
        AddRow(sb, "Level", ev.Level.ToString());
        AddRow(sb, "Application", ev.SourceApplication ?? "Unknown");
        AddRow(sb, "Environment", ev.SourceEnvironment ?? "");
        AddRow(sb, "Message", ev.RenderedMessage);
        if (!string.IsNullOrEmpty(ev.TraceId)) AddRow(sb, "Trace ID", ev.TraceId);
        sb.AppendLine("</table>");

        if (!string.IsNullOrEmpty(ev.Exception))
        {
            sb.AppendLine("<h3>Exception</h3>");
            sb.AppendLine($"<pre style=\"background:#f8f8f8;padding:10px;overflow:auto;\">{HtmlEncode(ev.Exception)}</pre>");
        }

        // Top 10 properties
        if (!string.IsNullOrEmpty(ev.PropertiesJson) && ev.PropertiesJson != "{}")
        {
            try
            {
                var props = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ev.PropertiesJson);
                if (props is { Count: > 0 })
                {
                    sb.AppendLine("<h3>Properties</h3><table style=\"border-collapse:collapse;\">");
                    foreach (var kv in props.Take(10))
                    {
                        sb.AppendLine($"<tr><td style=\"padding:4px 8px;font-weight:bold;\">{HtmlEncode(kv.Key)}</td>");
                        sb.AppendLine($"<td style=\"padding:4px 8px;\">{HtmlEncode(kv.Value.ToString())}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }
            }
            catch { /* ignore malformed JSON */ }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string BuildTextBody(AlertRule rule, LogEvent ev)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"LogVault Alert: {rule.Name}");
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Time:        {ev.Timestamp:u}");
        sb.AppendLine($"Level:       {ev.Level}");
        sb.AppendLine($"Application: {ev.SourceApplication ?? "Unknown"}");
        sb.AppendLine($"Message:     {ev.RenderedMessage}");
        if (!string.IsNullOrEmpty(ev.Exception))
        {
            sb.AppendLine();
            sb.AppendLine("Exception:");
            sb.AppendLine(ev.Exception);
        }
        return sb.ToString();
    }

    private static void AddRow(StringBuilder sb, string label, string value)
        => sb.AppendLine($"<tr><td style=\"padding:4px 8px;font-weight:bold;vertical-align:top;\">{HtmlEncode(label)}</td>" +
                         $"<td style=\"padding:4px 8px;\">{HtmlEncode(value)}</td></tr>");

    private static string HtmlEncode(string? s) =>
        s is null ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
