using LogVault.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LogVault.Application.Parsing;

/// <summary>
/// Parses OTLP/HTTP JSON log payloads (application/json content-type).
/// For binary protobuf support (application/x-protobuf), add Google.Protobuf
/// and include the opentelemetry-proto generated C# classes.
/// </summary>
public static class OtlpPayloadParser
{
    public static IReadOnlyList<LogEvent> ParseJson(string json)
    {
        var events = new List<LogEvent>();

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch { return events; }

        var resourceLogs = root?["resourceLogs"]?.AsArray()
                        ?? root?["resource_logs"]?.AsArray();
        if (resourceLogs == null) return events;

        foreach (var rl in resourceLogs)
        {
            var appName = ExtractServiceName(rl?["resource"]);

            var scopeLogsArr = rl?["scopeLogs"]?.AsArray()
                            ?? rl?["scope_logs"]?.AsArray();
            if (scopeLogsArr == null) continue;

            foreach (var sl in scopeLogsArr)
            {
                var logRecordsArr = sl?["logRecords"]?.AsArray()
                                 ?? sl?["log_records"]?.AsArray();
                if (logRecordsArr == null) continue;

                foreach (var lr in logRecordsArr)
                {
                    var ev = ParseLogRecord(lr, appName);
                    if (ev != null) events.Add(ev);
                }
            }
        }

        return events;
    }

    private static string? ExtractServiceName(JsonNode? resource)
    {
        var attrs = resource?["attributes"]?.AsArray();
        if (attrs == null) return null;
        foreach (var attr in attrs)
        {
            if (attr?["key"]?.GetValue<string>() == "service.name")
                return attr["value"]?["stringValue"]?.GetValue<string>();
        }
        return null;
    }

    private static LogEvent? ParseLogRecord(JsonNode? lr, string? appName)
    {
        if (lr == null) return null;

        // Timestamp: prefer timeUnixNano, fall back to observedTimeUnixNano
        var timeNanoStr = lr["timeUnixNano"]?.GetValue<string>()
                       ?? lr["time_unix_nano"]?.GetValue<string>()
                       ?? lr["observedTimeUnixNano"]?.GetValue<string>()
                       ?? lr["observed_time_unix_nano"]?.GetValue<string>();

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        if (ulong.TryParse(timeNanoStr, out var timeNano) && timeNano > 0)
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)(timeNano / 1_000_000));

        // Severity
        var severityNum = lr["severityNumber"]?.GetValue<int>()
                       ?? lr["severity_number"]?.GetValue<int>()
                       ?? 0;
        var severityText = lr["severityText"]?.GetValue<string>()
                        ?? lr["severity_text"]?.GetValue<string>();
        var level = MapSeverity(severityNum, severityText);

        // Body → message
        var body = lr["body"]?["stringValue"]?.GetValue<string>()
                ?? lr["body"]?["string_value"]?.GetValue<string>()
                ?? "";

        // Attributes → properties
        var propsObj = new JsonObject();
        var attrs = lr["attributes"]?.AsArray();
        if (attrs != null)
        {
            foreach (var attr in attrs)
            {
                var key = attr?["key"]?.GetValue<string>();
                if (string.IsNullOrEmpty(key)) continue;
                var val = attr?["value"];
                propsObj[key] = ExtractAnyValue(val);
            }
        }

        // TraceId and SpanId: OTLP JSON encodes as hex strings
        var traceId = lr["traceId"]?.GetValue<string>()
                   ?? lr["trace_id"]?.GetValue<string>();
        var spanId = lr["spanId"]?.GetValue<string>()
                  ?? lr["span_id"]?.GetValue<string>();

        // Some exporters send base64-encoded bytes instead of hex
        traceId = NormalizeId(traceId, expectedHexLen: 32);
        spanId = NormalizeId(spanId, expectedHexLen: 16);

        return new LogEvent
        {
            Timestamp = timestamp,
            Level = level,
            MessageTemplate = body,
            RenderedMessage = body,
            SourceApplication = appName,
            PropertiesJson = propsObj.Count > 0 ? propsObj.ToJsonString() : "{}",
            TraceId = string.IsNullOrEmpty(traceId) ? null : traceId,
            SpanId = string.IsNullOrEmpty(spanId) ? null : spanId,
            IngestedAt = DateTimeOffset.UtcNow
        };
    }

    private static JsonNode? ExtractAnyValue(JsonNode? value)
    {
        if (value == null) return null;
        if (value["stringValue"] is { } sv) return JsonValue.Create(sv.GetValue<string>());
        if (value["string_value"] is { } sv2) return JsonValue.Create(sv2.GetValue<string>());
        if (value["intValue"] is { } iv) return JsonValue.Create(iv.GetValue<long>());
        if (value["int_value"] is { } iv2) return JsonValue.Create(iv2.GetValue<long>());
        if (value["doubleValue"] is { } dv) return JsonValue.Create(dv.GetValue<double>());
        if (value["double_value"] is { } dv2) return JsonValue.Create(dv2.GetValue<double>());
        if (value["boolValue"] is { } bv) return JsonValue.Create(bv.GetValue<bool>());
        if (value["bool_value"] is { } bv2) return JsonValue.Create(bv2.GetValue<bool>());
        return JsonValue.Create(value.ToJsonString());
    }

    private static string? NormalizeId(string? id, int expectedHexLen)
    {
        if (string.IsNullOrEmpty(id)) return null;
        // Already correct hex length
        if (id.Length == expectedHexLen) return id.ToLowerInvariant();
        // Try base64 decode → hex
        try
        {
            var bytes = Convert.FromBase64String(id);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
        catch { return id; }
    }

    private static Domain.Entities.LogLevel MapSeverity(int severityNumber, string? text) =>
        severityNumber switch
        {
            >= 1 and <= 4 => Domain.Entities.LogLevel.Verbose,
            >= 5 and <= 8 => Domain.Entities.LogLevel.Debug,
            >= 9 and <= 12 => Domain.Entities.LogLevel.Information,
            >= 13 and <= 16 => Domain.Entities.LogLevel.Warning,
            >= 17 and <= 20 => Domain.Entities.LogLevel.Error,
            >= 21 => Domain.Entities.LogLevel.Fatal,
            _ => ParseLevelFromText(text)
        };

    private static Domain.Entities.LogLevel ParseLevelFromText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Domain.Entities.LogLevel.Information;
        return text.ToUpperInvariant() switch
        {
            "TRACE" or "TRACE2" or "TRACE3" or "TRACE4" => Domain.Entities.LogLevel.Verbose,
            "DEBUG" or "DEBUG2" or "DEBUG3" or "DEBUG4" => Domain.Entities.LogLevel.Debug,
            "INFO" or "INFORMATION" => Domain.Entities.LogLevel.Information,
            "WARN" or "WARNING" => Domain.Entities.LogLevel.Warning,
            "ERROR" => Domain.Entities.LogLevel.Error,
            "FATAL" or "CRITICAL" => Domain.Entities.LogLevel.Fatal,
            _ => Domain.Entities.LogLevel.Information
        };
    }
}
