namespace LogVault.Domain.Services;

public interface ISourceApplicationResolver
{
    string Resolve(
        string? propertyValue,
        string? headerValue,
        string? apiKeyDefault,
        string? explicitOverride);
}
