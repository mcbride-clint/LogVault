using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages;

public class DashboardModel(IDashboardRepository dashboards) : PageModel
{
    public int DashboardId { get; private set; }
    public IReadOnlyList<DashboardWidget> Widgets { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var ownerId = User.Identity?.Name ?? "anonymous";
        var dashboard = await dashboards.GetOrCreateDefaultAsync(ownerId, ct);
        DashboardId = dashboard.Id;
        Widgets = dashboard.Widgets.OrderBy(w => w.SortOrder).ToList();
    }
}
