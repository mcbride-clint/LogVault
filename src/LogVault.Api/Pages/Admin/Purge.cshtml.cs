using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class PurgeModel(ILogEventRepository logRepo) : PageModel
{
    [BindProperty] public DateTimeOffset? Before { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostPurgeAsync(CancellationToken ct)
    {
        if (!Before.HasValue)
        {
            TempData["Error"] = "Please specify a cutoff date.";
            return RedirectToPage();
        }

        var deleted = await logRepo.DeleteOlderThanAsync(Before.Value, ct);
        TempData["Success"] = $"Deleted {deleted:N0} log event(s) older than {Before.Value.ToLocalTime():yyyy-MM-dd HH:mm}.";
        return RedirectToPage();
    }
}
