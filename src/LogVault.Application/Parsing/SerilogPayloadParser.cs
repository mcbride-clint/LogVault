using LogVault.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LogVault.Application.Parsing;

public static class SerilogPayloadParser
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    // Parses Serilog HTTP sink batch format: { "events": [...] }
    public static IReadOnlyList<LogEvent> ParseBatch(string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
            return Array.Empty<LogEvent>();

        try
        {
            var doc = JsonNode.Parse(jsonPayload);
            if (doc == null) return Array.Empty<LogEvent>();

            var events = doc["events"] as JsonArray ?? doc["Events"] as JsonArray;
            if (events == null) return Array.Empty<LogEvent>();

            var result = new List<LogEvent>(events.Count);
            foreach (var node in events)
            {
                if (node is JsonObject obj)
                {
                    var ev = ParseEvent(obj);
                    if (ev != null) result.Add(ev);
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<LogEvent>();
        }
    }

    // Parses CLEF format: one JSON object per line
    public static IReadOnlyList<LogEvent> ParseClef(Stream stream)
    {
        var result = new List<LogEvent>();
        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var obj = JsonNode.Parse(line) as JsonObject;
                if (obj != null)
                {
                    var ev = ParseEvent(obj);
                    if (ev != null) result.Add(ev);
                }
            }
            catch { /* skip malformed lines */ }
        }
        return result;
    }

    private static LogEvent? ParseEvent(JsonObject obj)
    {
        try
        {
            var timestampStr = obj["@t"]?.GetValue<string>() ?? obj["Timestamp"]?.GetValue<string>();
            if (!DateTimeOffset.TryParse(timestampStr, out var timestamp))
                timestamp = DateTimeOffset.UtcNow;

            var levelStr = obj["@l"]?.GetValue<string>() ?? obj["Level"]?.GetValue<string>() ?? "Information";
            var level = ParseLevel(levelStr);

            var messageTemplate = obj["@mt"]?.GetValue<string>() ?? obj["MessageTemplate"]?.GetValue<string>() ?? "";
            var renderedMessage = obj["@m"]?.GetValue<string>() ?? obj["RenderedMessage"]?.GetValue<string>() ?? messageTemplate;
            var exception = obj["@x"]?.GetValue<string>() ?? obj["Exception"]?.GetValue<string>();

            // Extract well-known top-level properties
            var sourceApp = obj["Application"]?.GetValue<string>() ?? obj["SourceContext"]?.GetValue<string>();
            var traceId = obj["TraceId"]?.GetValue<string>();
            var spanId = obj["SpanId"]?.GetValue<string>();
            var host = obj["MachineName"]?.GetValue<string>();
            var env = obj["Environment"]?.GetValue<string>();

            // Build properties bag excluding CLEF meta fields
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "@t", "@mt", "@m", "@l", "@x", "@i", "@r",
                "Timestamp", "MessageTemplate", "RenderedMessage", "Level", "Exception"
            };

            var propsObj = new JsonObject();
            foreach (var kv in obj)
            {
                if (!excluded.Contains(kv.Key))
                    propsObj[kv.Key] = kv.Value?.DeepClone();
            }

            return new LogEvent
            {
                Timestamp = timestamp,
                Level = level,
                MessageTemplate = messageTemplate,
                RenderedMessage = renderedMessage,
                Exception = exception,
                SourceApplication = sourceApp,
                SourceEnvironment = env,
                SourceHost = host,
                PropertiesJson = propsObj.ToJsonString(),
                TraceId = traceId,
                SpanId = spanId,
                IngestedAt = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private static Domain.Entities.LogLevel ParseLevel(string level) => level.ToLowerInvariant() switch
    {
        "verbose" or "trace" => Domain.Entities.LogLevel.Verbose,
        "debug" => Domain.Entities.LogLevel.Debug,
        "information" or "info" => Domain.Entities.LogLevel.Information,
        "warning" or "warn" => Domain.Entities.LogLevel.Warning,
        "error" => Domain.Entities.LogLevel.Error,
        "fatal" or "critical" => Domain.Entities.LogLevel.Fatal,
        _ => Domain.Entities.LogLevel.Information
    };
}
