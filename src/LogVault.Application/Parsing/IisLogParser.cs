using LogVault.Domain.Entities;

namespace LogVault.Application.Parsing;

public static class IisLogParser
{
    public static IReadOnlyList<LogEvent> Parse(Stream stream)
    {
        var result = new List<LogEvent>();
        string[]? fields = null;

        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase))
            {
                fields = line[8..].Trim().Split(' ');
                continue;
            }

            if (line.StartsWith('#')) continue;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (fields == null) continue;

            var parts = line.Split(' ');
            if (parts.Length < fields.Length) continue;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fields.Length && i < parts.Length; i++)
                dict[fields[i]] = parts[i];

            var ev = BuildEvent(dict);
            if (ev != null) result.Add(ev);
        }

        return result;
    }

    private static LogEvent? BuildEvent(Dictionary<string, string> d)
    {
        try
        {
            var dateStr = d.GetValueOrDefault("date", "");
            var timeStr = d.GetValueOrDefault("time", "");
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(dateStr) && !string.IsNullOrEmpty(timeStr))
                DateTimeOffset.TryParse($"{dateStr}T{timeStr}Z", out timestamp);

            var statusStr = d.GetValueOrDefault("sc-status", "200");
            int.TryParse(statusStr, out var status);
            var level = status >= 500 ? Domain.Entities.LogLevel.Error
                      : status >= 400 ? Domain.Entities.LogLevel.Warning
                      : Domain.Entities.LogLevel.Information;

            var method = d.GetValueOrDefault("cs-method", "-");
            var uriStem = d.GetValueOrDefault("cs-uri-stem", "-");
            var uriQuery = d.GetValueOrDefault("cs-uri-query", "");
            var timeTaken = d.GetValueOrDefault("time-taken", "-");
            var serverIp = d.GetValueOrDefault("s-ip", "-");
            var clientIp = d.GetValueOrDefault("c-ip", "-");
            var host = d.GetValueOrDefault("cs-host", serverIp);

            var uriWithQuery = string.IsNullOrEmpty(uriQuery) || uriQuery == "-"
                ? uriStem
                : $"{uriStem}?{uriQuery}";

            var renderedMessage = $"{method} {uriWithQuery} {status} ({timeTaken}ms)";

            // Build full properties from all IIS fields
            var propsLines = string.Join(",", d.Select(kv => $"\"{kv.Key}\":\"{kv.Value}\""));
            var propsJson = "{" + propsLines + "}";

            return new LogEvent
            {
                Timestamp = timestamp,
                Level = level,
                MessageTemplate = "{Method} {UriStem} {Status}",
                RenderedMessage = renderedMessage,
                SourceHost = host,
                PropertiesJson = propsJson,
                IngestedAt = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }
}
