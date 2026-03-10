using LogVault.Domain.Services;

namespace LogVault.Application.Services;

public class SourceApplicationResolver : ISourceApplicationResolver
{
    // Priority: explicitOverride > propertyValue > headerValue > apiKeyDefault > "Unknown"
    public string Resolve(
        string? propertyValue,
        string? headerValue,
        string? apiKeyDefault,
        string? explicitOverride)
    {
        if (!string.IsNullOrWhiteSpace(explicitOverride)) return explicitOverride;
        if (!string.IsNullOrWhiteSpace(propertyValue)) return propertyValue;
        if (!string.IsNullOrWhiteSpace(headerValue)) return headerValue;
        if (!string.IsNullOrWhiteSpace(apiKeyDefault)) return apiKeyDefault;
        return "Unknown";
    }
}
