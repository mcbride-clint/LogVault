namespace LogVault.Domain.Models;

public record IngestContext(
    string? XApplicationName,
    string? XEnvironment,
    string? TraceParentHeader,
    string? ApiKeyDefaultApp,
    string? ExplicitApplication,
    string? ExplicitEnvironment
);
