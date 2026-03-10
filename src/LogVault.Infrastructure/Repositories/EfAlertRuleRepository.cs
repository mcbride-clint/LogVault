using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfAlertRuleRepository : IAlertRuleRepository
{
    private readonly LogVaultDbContext _db;

    public EfAlertRuleRepository(LogVaultDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertRule>> GetAllEnabledAsync(CancellationToken ct = default)
        => await _db.AlertRules
            .Include(r => r.Recipients)
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

    public async Task<AlertRule?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.AlertRules
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<AlertRule>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => await _db.AlertRules
            .Include(r => r.Recipients)
            .Where(r => r.OwnerId == ownerId)
            .ToListAsync(ct);

    public async Task<AlertRule> UpsertAsync(AlertRule rule, CancellationToken ct = default)
    {
        if (rule.Id == 0)
        {
            _db.AlertRules.Add(rule);
        }
        else
        {
            var existing = await _db.AlertRules
                .Include(r => r.Recipients)
                .FirstOrDefaultAsync(r => r.Id == rule.Id, ct);

            if (existing is null) throw new KeyNotFoundException($"AlertRule {rule.Id} not found");

            existing.Name = rule.Name;
            existing.FilterExpression = rule.FilterExpression;
            existing.MinimumLevel = rule.MinimumLevel;
            existing.SourceApplicationFilter = rule.SourceApplicationFilter;
            existing.ThrottleMinutes = rule.ThrottleMinutes;
            existing.IsEnabled = rule.IsEnabled;

            _db.AlertRecipients.RemoveRange(existing.Recipients);
            existing.Recipients = rule.Recipients;
        }

        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _db.AlertRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task UpdateLastFiredAsync(int id, DateTimeOffset firedAt, CancellationToken ct = default)
    {
        await _db.AlertRules
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastFiredAt, firedAt), ct);
    }
}
