using System.Net.Http.Json;

namespace LogVault.Client.Services;

public interface ILogVaultApiClient
{
    Task<UserInfo?> GetMeAsync();
    Task<PagedResult<LogEventDto>?> QueryLogsAsync(LogQuery query);
    Task<LogEventDto?> GetLogByIdAsync(long id);
    Task<LogStats?> GetStatsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<List<string>?> GetApplicationsAsync();
    Task<List<AlertRuleDto>?> GetAlertsAsync();
    Task<AlertRuleDto?> CreateAlertAsync(AlertRuleRequest request);
    Task<AlertRuleDto?> UpdateAlertAsync(int id, AlertRuleRequest request);
    Task DeleteAlertAsync(int id);
    Task<List<AlertFiredDto>?> GetAlertHistoryAsync(int id);
    Task<TestExpressionResult?> TestExpressionAsync(string expression);
    Task<List<ApiKeyDto>?> GetApiKeysAsync();
    Task<CreateApiKeyResult?> CreateApiKeyAsync(string label, string? defaultApplication);
    Task<RotateApiKeyResult?> RotateApiKeyAsync(int id);
    Task RevokeApiKeyAsync(int id);
    Task<PurgeResult?> TriggerRetentionAsync();
    Task<PurgeResult?> PurgeLogsAsync(DateTimeOffset before);
}

// DTOs
public record UserInfo(string? Username, string? DisplayName, string? Email, List<string> Roles, string AuthMethod);
public record LogEventDto(long Id, DateTimeOffset Timestamp, string Level, string? SourceApplication,
    string? SourceEnvironment, string RenderedMessage, string? Exception, string PropertiesJson,
    string? TraceId, string? SpanId, DateTimeOffset IngestedAt);
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
public record LogStats(List<LevelCount> ByLevel, List<HourlyCount> ByHour);
public record LevelCount(string Level, int Count);
public record HourlyCount(DateTimeOffset Hour, int Count);
public record AlertRuleDto(int Id, string Name, string OwnerId, string FilterExpression,
    string MinimumLevel, string? SourceApplicationFilter, int ThrottleMinutes,
    DateTimeOffset? LastFiredAt, bool IsEnabled, List<string> RecipientEmails);
public record AlertRuleRequest(string Name, string FilterExpression, string MinimumLevel,
    string? SourceApplicationFilter, int ThrottleMinutes, bool IsEnabled, List<string> RecipientEmails);
public record AlertFiredDto(long Id, int AlertRuleId, long TriggeringEventId, DateTimeOffset FiredAt, bool EmailSent);
public record TestExpressionResult(bool Valid, string? Error);
public record ApiKeyDto(int Id, string Label, string? DefaultApplication, bool IsEnabled,
    bool IsRevoked, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
public record CreateApiKeyResult(int Id, string RawKey, string Label, string? DefaultApplication);
public record RotateApiKeyResult(int NewKeyId, string RawKey, DateTimeOffset? OldKeyExpiresAt);
public record PurgeResult(int DeletedCount);

public record LogQuery(
    DateTimeOffset? From = null, DateTimeOffset? To = null,
    string? Level = null, string? App = null, string? Env = null,
    string? Q = null, string? TraceId = null,
    string? Prop = null, string? PropValue = null,
    int Page = 1, int PageSize = 50, bool Desc = true);
