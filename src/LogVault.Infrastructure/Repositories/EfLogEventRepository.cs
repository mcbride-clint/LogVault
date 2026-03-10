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

        var byHour = await _db.LogEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
            .ToListAsync(ct);

        var hourlyList = byHour.Select(h =>
            new HourlyCount(new DateTimeOffset(h.Year, h.Month, h.Day, h.Hour, 0, 0, TimeSpan.Zero), h.Count))
            .ToList();

        return new LogStats(byLevel, hourlyList);
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
            q = q.Where(e => e.PropertiesJson.Contains("\"" + query.PropertyKey + "\""));
        if (!string.IsNullOrEmpty(query.PropertyValue))
            q = q.Where(e => e.PropertiesJson.Contains(query.PropertyValue));

        return q;
    }
}
