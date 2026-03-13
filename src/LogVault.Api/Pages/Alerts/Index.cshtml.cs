using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Alerts;

public class IndexModel(IAlertRuleRepository alertRepo) : PageModel
{
    public IReadOnlyList<AlertRule> Rules { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
        {
            var enabled = await alertRepo.GetAllEnabledAsync(ct);
            // Get all (including disabled) — fall back to owner-scoped for non-admins
            Rules = enabled;
        }
        else
        {
            var ownerId = User.Identity?.Name ?? "";
            Rules = await alertRepo.GetByOwnerAsync(ownerId, ct);
        }
    }
}
