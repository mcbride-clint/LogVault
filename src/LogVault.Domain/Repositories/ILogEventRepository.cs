using LogVault.Domain.Entities;
using LogVault.Domain.Models;

namespace LogVault.Domain.Repositories;

public interface ILogEventRepository
{
    Task<long> InsertAsync(LogEvent logEvent, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<LogEvent> events, CancellationToken ct = default);
    Task<PagedResult<LogEvent>> QueryAsync(LogEventQuery query, CancellationToken ct = default);
    Task<LogEvent?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
    IAsyncEnumerable<LogEvent> StreamAsync(LogEventQuery query, CancellationToken ct = default);
    Task<LogStats> GetStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctApplicationsAsync(CancellationToken ct = default);
}
