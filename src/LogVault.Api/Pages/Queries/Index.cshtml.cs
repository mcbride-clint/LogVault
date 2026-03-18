using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Queries;

public class IndexModel(ISavedFilterRepository repo) : PageModel
{
    public IReadOnlyList<SavedFilter> Queries { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var ownerId = User.Identity?.Name ?? "";
        Queries = User.IsInRole("Admin")
            ? await repo.GetAllAsync(ct)
            : await repo.GetAllAccessibleAsync(ownerId, ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        var ownerId = User.Identity?.Name ?? "";
        var filter = await repo.GetByIdAsync(id, ct);
        if (filter != null && filter.OwnerId == ownerId)
            await repo.DeleteAsync(id, ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string newName, CancellationToken ct)
    {
        var ownerId = User.Identity?.Name ?? "";
        var filter = await repo.GetByIdAsync(id, ct);
        if (filter != null && filter.OwnerId == ownerId && !string.IsNullOrWhiteSpace(newName))
        {
            filter.Name = newName.Trim();
            await repo.UpsertAsync(filter, ct);
        }
        return RedirectToPage();
    }
}
