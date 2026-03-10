using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Domain.Services;
using Microsoft.Extensions.Logging;

namespace LogVault.Application.Alerts;

public class AlertEvaluationService : IAlertEvaluationService
{
    private readonly IAlertRuleRepository _alertRules;
    private readonly IAlertFiredRepository _alertFired;
    private readonly IAlertEmailService _emailService;
    private readonly AlertRuleExpressionCache _cache;
    private readonly ILogger<AlertEvaluationService> _logger;

    public AlertEvaluationService(
        IAlertRuleRepository alertRules,
        IAlertFiredRepository alertFired,
        IAlertEmailService emailService,
        AlertRuleExpressionCache cache,
        ILogger<AlertEvaluationService> logger)
    {
        _alertRules = alertRules;
        _alertFired = alertFired;
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    public async Task EvaluateAsync(IReadOnlyList<LogEvent> newEvents, CancellationToken ct = default)
    {
        if (newEvents.Count == 0) return;

        var rules = await _alertRules.GetAllEnabledAsync(ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var rule in rules)
        {
            if (!IsThrottleAllowed(rule, now)) continue;

            var predicate = _cache.GetOrCompile(rule);

            LogEvent? match = null;
            foreach (var ev in newEvents)
            {
                try
                {
                    if (predicate(ev)) { match = ev; break; }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error evaluating alert rule {RuleId}", rule.Id);
                }
            }

            if (match == null) continue;

            await FireAlertAsync(rule, match, now, ct);
        }
    }

    private static bool IsThrottleAllowed(AlertRule rule, DateTimeOffset now)
    {
        if (!rule.LastFiredAt.HasValue) return true;
        return (now - rule.LastFiredAt.Value).TotalMinutes >= rule.ThrottleMinutes;
    }

    private async Task FireAlertAsync(AlertRule rule, LogEvent ev, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var fired = new AlertFired
            {
                AlertRuleId = rule.Id,
                TriggeringEventId = ev.Id,
                FiredAt = now,
                EmailSent = false
            };

            await _alertFired.RecordAsync(fired, ct);
            await _alertRules.UpdateLastFiredAsync(rule.Id, now, ct);

            try
            {
                await _emailService.SendAlertAsync(rule, ev, ct);
                _logger.LogInformation("Alert fired: rule {RuleName} matched event {EventId}", rule.Name, ev.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert email for rule {RuleId}", rule.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing alert for rule {RuleId}", rule.Id);
        }
    }
}
