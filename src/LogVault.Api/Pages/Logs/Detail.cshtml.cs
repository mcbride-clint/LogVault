using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Logs;

public class DetailModel(ILogEventRepository repo) : PageModel
{
    public LogEvent Event { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct)
    {
        var ev = await repo.GetByIdAsync(id, ct);
        if (ev is null) return NotFound();
        Event = ev;
        return Page();
    }
}
