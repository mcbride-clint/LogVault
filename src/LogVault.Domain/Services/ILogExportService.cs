using LogVault.Domain.Models;

namespace LogVault.Domain.Services;

public interface ILogExportService
{
    Task ExportJsonAsync(LogEventQuery query, Stream outputStream, CancellationToken ct = default);
    Task ExportCsvAsync(LogEventQuery query, Stream outputStream, CancellationToken ct = default);
}
