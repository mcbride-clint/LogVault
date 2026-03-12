using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using LogVault.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogVault.Infrastructure.Repositories;

public class EfLogEventRepository : ILogEventRepository
{
    private readonly LogVaultDbContext _db;

    public EfLogEventRepository(LogVaultDbContext db) => _db = db;

    public async Task<long> InsertAsync(LogEvent logEvent, CancellationToken ct = default)
    {
        _db.LogEvents.Add(logEvent);
        await _db.SaveChangesAsync(ct);
        return logEvent.Id;
    }

    public async Task BulkInsertAsync(IEnumerable<LogEvent> events, CancellationToken ct = default)
    {
        _db.LogEvents.AddRange(events);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<LogEvent>> QueryAsync(LogEventQuery query, CancellationToken ct = default)
    {
        var q = BuildQuery(query);
        var total = await q.CountAsync(ct);

        q = query.Descending
            ? q.OrderByDescending(e => e.Timestamp)
            : q.OrderBy(e => e.Timestamp);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<LogEvent>(items, total, query.Page, query.PageSize);
    }

    public async Task<LogEvent?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _db.LogEvents.FindAsync([id], ct);

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => await _db.LogEvents.Where(e => e.Timestamp < cutoff).ExecuteDeleteAsync(ct);

    public IAsyncEnumerable<LogEvent> StreamAsync(LogEventQuery query, CancellationToken ct = default)
    {
        var q = BuildQuery(query);
        q = query.Descending
            ? q.OrderByDescending(e => e.Timestamp)
            : q.OrderBy(e => e.Timestamp);
        return q.AsAsyncEnumerable();
    }

    public async Task<LogStats> GetStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var byLevel = await _db.LogEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .GroupBy(e => e.Level)
            .Select(g => new LevelCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var timestamps = await _db.LogEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .Select(e => e.Timestamp)
            .ToListAsync(ct);

        var hourlyList = timestamps
            .GroupBy(ts => new { ts.Year, ts.Month, ts.Day, ts.Hour })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ThenBy(g => g.Key.Day).ThenBy(g => g.Key.Hour)
            .Select(g => new HourlyCount(new DateTimeOffset(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0, TimeSpan.Zero), g.Count()))
            .ToList();

        return new LogStats(byLevel, hourlyList);
    }

    public async Task<IReadOnlyList<AppCount>> GetTopApplicationsAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct = default)
    {
        var rows = await _db.LogEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to && e.SourceApplication != null)
            .GroupBy(e => e.SourceApplication!)
            .Select(g => new { App = g.Key, Count = g.Count() })
            .OrderByDescending(a => a.Count)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => new AppCount(r.App, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetDistinctApplicationsAsync(CancellationToken ct = default)
        => await _db.LogEvents
            .Where(e => e.SourceApplication != null)
            .Select(e => e.SourceApplication!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

    private IQueryable<LogEvent> BuildQuery(LogEventQuery query)
    {
        var q = _db.LogEvents.AsQueryable();

        if (query.From.HasValue) q = q.Where(e => e.Timestamp >= query.From.Value);
        if (query.To.HasValue) q = q.Where(e => e.Timestamp <= query.To.Value);
        if (query.MinLevel.HasValue) q = q.Where(e => e.Level >= query.MinLevel.Value);
        if (query.MaxLevel.HasValue) q = q.Where(e => e.Level <= query.MaxLevel.Value);
        if (!string.IsNullOrEmpty(query.SourceApplication))
            q = q.Where(e => e.SourceApplication != null && e.SourceApplication.Contains(query.SourceApplication));
        if (!string.IsNullOrEmpty(query.SourceEnvironment))
            q = q.Where(e => e.SourceEnvironment == query.SourceEnvironment);
        if (!string.IsNullOrEmpty(query.MessageContains))
            q = q.Where(e => e.RenderedMessage.Contains(query.MessageContains));
        if (!string.IsNullOrEmpty(query.ExceptionContains))
            q = q.Where(e => e.Exception != null && e.Exception.Contains(query.ExceptionContains));
        if (!string.IsNullOrEmpty(query.TraceId))
            q = q.Where(e => e.TraceId == query.TraceId);
        if (!string.IsNullOrEmpty(query.PropertyKey))
        {
            var key = query.PropertyKey;
            var val = query.PropertyValue ?? "";
            switch (query.PropertyOp)
            {
                case Domain.Models.PropertyFilterOp.Equals:
                    q = q.Where(e =>
                        EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":\"{val}\"%") ||
                        EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":{val},%") ||
                        EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":{val}}}%"));
                    break;
                case Domain.Models.PropertyFilterOp.NotEquals:
                    q = q.Where(e =>
                        !EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":\"{val}\"%") &&
                        !EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":{val},%") &&
                        !EF.Functions.Like(e.PropertiesJson, $"%\"{key}\":{val}}}%") &&
                        e.PropertiesJson.Contains($"\"{key}\""));
                    break;
                case Domain.Models.PropertyFilterOp.Contains:
                default:
                    q = q.Where(e => e.PropertiesJson.Contains($"\"{key}\""));
                    if (!string.IsNullOrEmpty(val))
                        q = q.Where(e => e.PropertiesJson.Contains(val));
                    break;
            }
        }

        if (!string.IsNullOrEmpty(query.FullTextSearch))
        {
            foreach (var term in query.FullTextSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = term;
                q = q.Where(e =>
                    e.RenderedMessage.Contains(t) ||
                    (e.Exception != null && e.Exception.Contains(t)) ||
                    e.PropertiesJson.Contains(t));
            }
        }

        return q;
    }
}
