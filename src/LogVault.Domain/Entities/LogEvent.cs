namespace LogVault.Domain.Entities;

public class LogEvent
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string MessageTemplate { get; set; } = "";
    public string RenderedMessage { get; set; } = "";
    public string? Exception { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceEnvironment { get; set; }
    public string? SourceHost { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
}
