namespace LogVault.Domain.Models;

public record LogEventQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Domain.Entities.LogLevel? MinLevel,
    Domain.Entities.LogLevel? MaxLevel,
    string? SourceApplication,
    string? SourceEnvironment,
    string? MessageContains,
    string? ExceptionContains,
    string? PropertyKey,
    string? PropertyValue,
    string? TraceId,
    int Page,
    int PageSize,
    string SortBy,
    bool Descending,
    string? FullTextSearch = null,
    PropertyFilterOp PropertyOp = PropertyFilterOp.Contains
)
{
    public static LogEventQuery Default => new(
        From: null, To: null, MinLevel: null, MaxLevel: null,
        SourceApplication: null, SourceEnvironment: null,
        MessageContains: null, ExceptionContains: null,
        PropertyKey: null, PropertyValue: null, TraceId: null,
        Page: 1, PageSize: 50, SortBy: "Timestamp", Descending: true,
        FullTextSearch: null, PropertyOp: PropertyFilterOp.Contains);
}
