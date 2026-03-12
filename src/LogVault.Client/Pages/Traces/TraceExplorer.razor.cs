namespace LogVault.Client.Pages.Traces;

public partial class TraceExplorer
{
    private class TraceSpanGroup
    {
        public string? SpanId { get; }
        public string? Application { get; }
        public List<Services.LogEventDto> Events { get; } = [];

        public TraceSpanGroup(string? spanId, string? application)
        {
            SpanId = spanId;
            Application = application;
        }
    }
}
