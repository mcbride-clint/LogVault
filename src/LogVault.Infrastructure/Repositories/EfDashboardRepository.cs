using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfDashboardRepository : IDashboardRepository
{
    private readonly LogVaultDbContext _db;

    public EfDashboardRepository(LogVaultDbContext db) => _db = db;

    public async Task<IReadOnlyList<Dashboard>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await _db.Dashboards
            .Include(d => d.Widgets)
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.Name)
            .ToListAsync(ct);

    public async Task<Dashboard?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Dashboard> GetOrCreateDefaultAsync(string ownerId, CancellationToken ct = default)
    {
        var existing = await _db.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.OwnerId == ownerId && d.IsDefault, ct);

        if (existing != null) return existing;

        // Auto-create a seeded default dashboard
        var seed = new Dashboard
        {
            Name = "Default Dashboard",
            OwnerId = ownerId,
            IsDefault = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Widgets =
            [
                new DashboardWidget { WidgetType = "ByLevel",         Title = "Events by Level",        SortOrder = 0, ConfigJson = """{"hours":24}""" },
                new DashboardWidget { WidgetType = "EventRate",        Title = "Event Rate (last 12h)",  SortOrder = 1, ConfigJson = """{"hours":12}""" },
                new DashboardWidget { WidgetType = "ErrorList",        Title = "Recent Errors",          SortOrder = 2, ConfigJson = """{"hours":24,"limit":20}""" },
                new DashboardWidget { WidgetType = "TopApplications",  Title = "Top Applications",       SortOrder = 3, ConfigJson = """{"hours":24,"limit":10}""" },
            ]
        };

        _db.Dashboards.Add(seed);
        await _db.SaveChangesAsync(ct);
        return seed;
    }

    public async Task<Dashboard> CreateAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        if (dashboard.IsDefault)
            await ClearDefaultFlagAsync(dashboard.OwnerId, ct);

        _db.Dashboards.Add(dashboard);
        await _db.SaveChangesAsync(ct);
        return dashboard;
    }

    public async Task<Dashboard> UpdateAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        if (dashboard.IsDefault)
            await ClearDefaultFlagAsync(dashboard.OwnerId, ct, excludeId: dashboard.Id);

        _db.Dashboards.Update(dashboard);
        await _db.SaveChangesAsync(ct);
        return dashboard;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var d = await _db.Dashboards.FindAsync([id], ct);
        if (d != null)
        {
            _db.Dashboards.Remove(d);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> CountByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await _db.Dashboards.CountAsync(d => d.OwnerId == ownerId, ct);

    private async Task ClearDefaultFlagAsync(string ownerId, CancellationToken ct, int? excludeId = null)
    {
        var defaults = await _db.Dashboards
            .Where(d => d.OwnerId == ownerId && d.IsDefault && (excludeId == null || d.Id != excludeId))
            .ToListAsync(ct);

        foreach (var d in defaults)
            d.IsDefault = false;
    }
}
