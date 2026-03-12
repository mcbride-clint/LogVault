using System.Net.Http.Json;
using System.Text.Json;
using LogVault.Domain.Entities;
using LogVault.Domain.Services;
using Microsoft.Extensions.Logging;

namespace LogVault.Infrastructure.Services;

public class HttpWebhookNotificationService : IWebhookNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpWebhookNotificationService> _logger;

    public HttpWebhookNotificationService(IHttpClientFactory httpClientFactory, ILogger<HttpWebhookNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(AlertRule rule, LogEvent ev, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rule.WebhookUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient("webhook");
            object payload = rule.WebhookFormat switch
            {
                WebhookFormat.Slack => BuildSlackPayload(rule, ev),
                WebhookFormat.Teams => BuildTeamsPayload(rule, ev),
                _ => BuildGenericPayload(rule, ev)
            };

            var response = await client.PostAsJsonAsync(rule.WebhookUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Webhook for rule {RuleId} returned {StatusCode}", rule.Id, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook for alert rule {RuleId}", rule.Id);
        }
    }

    private static object BuildGenericPayload(AlertRule rule, LogEvent ev) => new
    {
        ruleName = rule.Name,
        level = ev.Level.ToString(),
        application = ev.SourceApplication,
        environment = ev.SourceEnvironment,
        message = ev.RenderedMessage,
        exception = ev.Exception,
        traceId = ev.TraceId,
        eventId = ev.Id,
        firedAt = DateTimeOffset.UtcNow
    };

    private static object BuildSlackPayload(AlertRule rule, LogEvent ev)
    {
        var color = ev.Level switch
        {
            Domain.Entities.LogLevel.Fatal or Domain.Entities.LogLevel.Error => "danger",
            Domain.Entities.LogLevel.Warning => "warning",
            _ => "good"
        };

        return new
        {
            text = $":rotating_light: *LogVault Alert: {rule.Name}*",
            attachments = new[]
            {
                new
                {
                    color,
                    fields = new[]
                    {
                        new { title = "Level", value = ev.Level.ToString(), @short = true },
                        new { title = "Application", value = ev.SourceApplication ?? "—", @short = true },
                        new { title = "Environment", value = ev.SourceEnvironment ?? "—", @short = true },
                        new { title = "Time", value = ev.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                        new { title = "Message", value = ev.RenderedMessage, @short = false }
                    },
                    footer = ev.TraceId != null ? $"Trace: {ev.TraceId}" : null
                }
            }
        };
    }

    private static object BuildTeamsPayload(AlertRule rule, LogEvent ev) => new
    {
        type = "MessageCard",
        context = "https://schema.org/extensions",
        themeColor = ev.Level switch
        {
            Domain.Entities.LogLevel.Fatal or Domain.Entities.LogLevel.Error => "FF0000",
            Domain.Entities.LogLevel.Warning => "FFA500",
            _ => "0078D4"
        },
        summary = $"LogVault Alert: {rule.Name}",
        sections = new[]
        {
            new
            {
                activityTitle = $"**{rule.Name}** fired",
                activitySubtitle = $"{ev.Level} in {ev.SourceApplication ?? "Unknown"}",
                facts = new[]
                {
                    new { name = "Level", value = ev.Level.ToString() },
                    new { name = "Application", value = ev.SourceApplication ?? "—" },
                    new { name = "Environment", value = ev.SourceEnvironment ?? "—" },
                    new { name = "Time", value = ev.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                    new { name = "Message", value = ev.RenderedMessage }
                },
                text = ev.Exception != null ? $"**Exception:** {ev.Exception[..Math.Min(500, ev.Exception.Length)]}" : null
            }
        }
    };
}
