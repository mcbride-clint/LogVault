using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfAlertFiredRepository : IAlertFiredRepository
{
    private readonly LogVaultDbContext _db;

    public EfAlertFiredRepository(LogVaultDbContext db) => _db = db;

    public async Task RecordAsync(AlertFired fired, CancellationToken ct = default)
    {
        _db.AlertsFired.Add(fired);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AlertFired>> GetRecentAsync(int alertRuleId, int count, CancellationToken ct = default)
        => await _db.AlertsFired
            .Where(f => f.AlertRuleId == alertRuleId)
            .OrderByDescending(f => f.FiredAt)
            .Take(count)
            .ToListAsync(ct);
}
