using LogVault.Domain.Models;

namespace LogVault.Domain.Services;

public enum LogFileFormat { SerilogJson, Clef, IIS, PlainText }

public interface ILogIngestionService
{
    Task<int> IngestSerilogBatchAsync(string jsonPayload, IngestContext context, CancellationToken ct = default);
    Task<int> IngestClefStreamAsync(Stream clefStream, IngestContext context, CancellationToken ct = default);
    Task<int> IngestFileAsync(Stream logStream, LogFileFormat format, IngestContext context, CancellationToken ct = default);
    Task<int> IngestOtlpJsonAsync(string jsonPayload, IngestContext context, CancellationToken ct = default);
}
