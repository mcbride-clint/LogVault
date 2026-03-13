using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LogLevel = LogVault.Domain.Entities.LogLevel;
using WebhookFormat = LogVault.Domain.Entities.WebhookFormat;
using AlertRule = LogVault.Domain.Entities.AlertRule;
using AlertRecipient = LogVault.Domain.Entities.AlertRecipient;

namespace LogVault.Api.Pages.Alerts;

public class EditModel(IAlertRuleRepository alertRepo) : PageModel
{
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string FilterExpression { get; set; } = "";
    [BindProperty] public LogLevel MinimumLevel { get; set; } = LogLevel.Error;
    [BindProperty] public string? SourceApplicationFilter { get; set; }
    [BindProperty] public int ThrottleMinutes { get; set; } = 60;
    [BindProperty] public bool IsEnabled { get; set; } = true;
    [BindProperty] public List<string> Recipients { get; set; } = [];
    [BindProperty] public string? WebhookUrl { get; set; }
    [BindProperty] public WebhookFormat WebhookFormat { get; set; } = WebhookFormat.Generic;

    public int? EditId { get; private set; }

    public async Task OnGetAsync(int? id, string? app, LogLevel? level, CancellationToken ct)
    {
        if (id.HasValue)
        {
            EditId = id;
            var rule = await alertRepo.GetByIdAsync(id.Value, ct);
            if (rule is not null)
            {
                Name = rule.Name;
                FilterExpression = rule.FilterExpression;
                MinimumLevel = rule.MinimumLevel;
                SourceApplicationFilter = rule.SourceApplicationFilter;
                ThrottleMinutes = rule.ThrottleMinutes;
                IsEnabled = rule.IsEnabled;
                Recipients = rule.Recipients.Select(r => r.Email).ToList();
                WebhookUrl = rule.WebhookUrl;
                WebhookFormat = rule.WebhookFormat;
            }
        }
        else
        {
            // Pre-fill from query params (coming from Log Detail page)
            if (app is not null) SourceApplicationFilter = app;
            if (level.HasValue) MinimumLevel = level.Value;
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var ownerId = User.Identity?.Name ?? "anonymous";
        var rule = id.HasValue ? (await alertRepo.GetByIdAsync(id.Value, ct) ?? new AlertRule()) : new AlertRule();

        rule.Name = Name;
        rule.OwnerId = ownerId;
        rule.FilterExpression = FilterExpression;
        rule.MinimumLevel = MinimumLevel;
        rule.SourceApplicationFilter = SourceApplicationFilter;
        rule.ThrottleMinutes = ThrottleMinutes;
        rule.IsEnabled = IsEnabled;
        rule.WebhookUrl = WebhookUrl;
        rule.WebhookFormat = WebhookFormat;
        rule.Recipients = Recipients
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => new AlertRecipient { Email = e.Trim() })
            .ToList();

        await alertRepo.UpsertAsync(rule, ct);
        TempData["Success"] = $"Alert rule '{Name}' saved.";
        return RedirectToPage("/Alerts/Index");
    }
}
