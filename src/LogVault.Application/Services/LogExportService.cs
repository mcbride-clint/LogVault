using CsvHelper;
using CsvHelper.Configuration;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using LogVault.Domain.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LogVault.Application.Services;

public class LogExportService : ILogExportService
{
    private readonly ILogEventRepository _repo;

    public LogExportService(ILogEventRepository repo) => _repo = repo;

    public async Task ExportJsonAsync(LogEventQuery query, Stream outputStream, CancellationToken ct = default)
    {
        var exportQuery = query with { Page = 1, PageSize = int.MaxValue };

        await using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = false });
        writer.WriteStartArray();

        await foreach (var ev in _repo.StreamAsync(exportQuery, ct))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", ev.Id);
            writer.WriteString("timestamp", ev.Timestamp);
            writer.WriteString("level", ev.Level.ToString());
            writer.WriteString("messageTemplate", ev.MessageTemplate);
            writer.WriteString("renderedMessage", ev.RenderedMessage);
            if (ev.Exception != null) writer.WriteString("exception", ev.Exception);
            if (ev.SourceApplication != null) writer.WriteString("sourceApplication", ev.SourceApplication);
            if (ev.SourceEnvironment != null) writer.WriteString("sourceEnvironment", ev.SourceEnvironment);
            if (ev.SourceHost != null) writer.WriteString("sourceHost", ev.SourceHost);
            writer.WriteString("propertiesJson", ev.PropertiesJson);
            if (ev.TraceId != null) writer.WriteString("traceId", ev.TraceId);
            if (ev.SpanId != null) writer.WriteString("spanId", ev.SpanId);
            writer.WriteString("ingestedAt", ev.IngestedAt);
            writer.WriteEndObject();

            await writer.FlushAsync(ct);
        }

        writer.WriteEndArray();
        await writer.FlushAsync(ct);
    }

    public async Task ExportCsvAsync(LogEventQuery query, Stream outputStream, CancellationToken ct = default)
    {
        var exportQuery = query with { Page = 1, PageSize = int.MaxValue };

        await using var streamWriter = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        await using var csv = new CsvWriter(streamWriter, config);

        csv.WriteHeader<LogEventCsvRecord>();
        await csv.NextRecordAsync();

        await foreach (var ev in _repo.StreamAsync(exportQuery, ct))
        {
            csv.WriteRecord(new LogEventCsvRecord(ev));
            await csv.NextRecordAsync();
            await streamWriter.FlushAsync(ct);
        }
    }
}

internal record LogEventCsvRecord
{
    public long Id { get; init; }
    public string Timestamp { get; init; }
    public string Level { get; init; }
    public string RenderedMessage { get; init; }
    public string? Exception { get; init; }
    public string? SourceApplication { get; init; }
    public string? SourceEnvironment { get; init; }
    public string? SourceHost { get; init; }
    public string? TraceId { get; init; }
    public string IngestedAt { get; init; }

    public LogEventCsvRecord(LogEvent ev)
    {
        Id = ev.Id;
        Timestamp = ev.Timestamp.ToString("o");
        Level = ev.Level.ToString();
        RenderedMessage = ev.RenderedMessage;
        Exception = ev.Exception;
        SourceApplication = ev.SourceApplication;
        SourceEnvironment = ev.SourceEnvironment;
        SourceHost = ev.SourceHost;
        TraceId = ev.TraceId;
        IngestedAt = ev.IngestedAt.ToString("o");
    }
}
