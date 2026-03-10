namespace LogVault.Application.Parsing;

public static class TraceParentParser
{
    // W3C traceparent format: 00-{traceId(32hex)}-{parentId(16hex)}-{flags(2hex)}
    public static (string? TraceId, string? SpanId) Parse(string? traceparentHeader)
    {
        if (string.IsNullOrWhiteSpace(traceparentHeader))
            return (null, null);

        var parts = traceparentHeader.Trim().Split('-');
        if (parts.Length < 4)
            return (null, null);

        var version = parts[0];
        var traceId = parts[1];
        var parentId = parts[2];

        if (version.Length != 2 || traceId.Length != 32 || parentId.Length != 16)
            return (null, null);

        if (!IsHex(traceId) || !IsHex(parentId))
            return (null, null);

        // All-zeros traceId is invalid per W3C spec
        if (traceId == "00000000000000000000000000000000")
            return (null, null);

        return (traceId, parentId);
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }
}
