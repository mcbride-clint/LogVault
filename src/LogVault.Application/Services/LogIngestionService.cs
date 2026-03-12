using LogVault.Application.Parsing;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using LogVault.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace LogVault.Application.Services;

public class LogIngestionService : ILogIngestionService
{
    private readonly Channel<IReadOnlyList<LogEvent>> _channel;
    private readonly ISourceApplicationResolver _sourceResolver;
    private readonly ILogger<LogIngestionService> _logger;

    public LogIngestionService(
        Channel<IReadOnlyList<LogEvent>> channel,
        ISourceApplicationResolver sourceResolver,
        ILogger<LogIngestionService> logger)
    {
        _channel = channel;
        _sourceResolver = sourceResolver;
        _logger = logger;
    }

    public async Task<int> IngestSerilogBatchAsync(string jsonPayload, IngestContext context, CancellationToken ct = default)
    {
        var events = SerilogPayloadParser.ParseBatch(jsonPayload);
        return await IngestEventsAsync(events, context, ct);
    }

    public async Task<int> IngestOtlpJsonAsync(string jsonPayload, IngestContext context, CancellationToken ct = default)
    {
        var events = OtlpPayloadParser.ParseJson(jsonPayload);
        return await IngestEventsAsync(events, context, ct);
    }

    public async Task<int> IngestClefStreamAsync(Stream clefStream, IngestContext context, CancellationToken ct = default)
    {
        var events = SerilogPayloadParser.ParseClef(clefStream);
        return await IngestEventsAsync(events, context, ct);
    }

    public async Task<int> IngestFileAsync(Stream logStream, LogFileFormat format, IngestContext context, CancellationToken ct = default)
    {
        IReadOnlyList<LogEvent> events = format switch
        {
            LogFileFormat.IIS => IisLogParser.Parse(logStream),
            LogFileFormat.Clef => SerilogPayloadParser.ParseClef(logStream),
            LogFileFormat.SerilogJson => ParseSerilogJsonStream(logStream),
            LogFileFormat.PlainText => ParsePlainText(logStream),
            _ => Array.Empty<LogEvent>()
        };

        return await IngestEventsAsync(events, context, ct);
    }

    private async Task<int> IngestEventsAsync(IReadOnlyList<LogEvent> events, IngestContext context, CancellationToken ct)
    {
        if (events.Count == 0) return 0;

        var (headerTraceId, headerSpanId) = TraceParentParser.Parse(context.TraceParentHeader);

        foreach (var ev in events)
        {
            // Apply source application resolution
            var propApp = ev.SourceApplication;
            ev.SourceApplication = _sourceResolver.Resolve(
                propertyValue: propApp,
                headerValue: context.XApplicationName,
                apiKeyDefault: context.ApiKeyDefaultApp,
                explicitOverride: context.ExplicitApplication);

            // Apply environment override
            if (!string.IsNullOrEmpty(context.ExplicitEnvironment))
                ev.SourceEnvironment = context.ExplicitEnvironment;
            else if (!string.IsNullOrEmpty(context.XEnvironment) && string.IsNullOrEmpty(ev.SourceEnvironment))
                ev.SourceEnvironment = context.XEnvironment;

            // Apply traceparent header as fallback
            if (string.IsNullOrEmpty(ev.TraceId) && headerTraceId != null)
                ev.TraceId = headerTraceId;
            if (string.IsNullOrEmpty(ev.SpanId) && headerSpanId != null)
                ev.SpanId = headerSpanId;

            ev.IngestedAt = DateTimeOffset.UtcNow;
        }

        // Try writing to channel; fall back to direct insert if full (handled by worker)
        if (_channel.Writer.TryWrite(events))
        {
            return events.Count;
        }

        // Channel is full — write will wait
        try
        {
            await _channel.Writer.WriteAsync(events, ct);
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Ingestion channel is closed; dropping {Count} events", events.Count);
            return 0;
        }

        return events.Count;
    }

    private static IReadOnlyList<LogEvent> ParseSerilogJsonStream(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var json = reader.ReadToEnd();
        return SerilogPayloadParser.ParseBatch(json);
    }

    private static IReadOnlyList<LogEvent> ParsePlainText(Stream stream)
    {
        var events = new List<LogEvent>();
        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            events.Add(new LogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = Domain.Entities.LogLevel.Information,
                MessageTemplate = line,
                RenderedMessage = line,
                PropertiesJson = "{}",
                IngestedAt = DateTimeOffset.UtcNow
            });
        }
        return events;
    }
}
