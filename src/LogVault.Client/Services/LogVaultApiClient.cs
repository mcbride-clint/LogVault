using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LogVault.Client.Services;

public class LogVaultApiClient : ILogVaultApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public LogVaultApiClient(HttpClient http) => _http = http;

    public async Task<UserInfo?> GetMeAsync()
    {
        try { return await _http.GetFromJsonAsync<UserInfo>("/api/auth/me", _opts); }
        catch { return null; }
    }

    public async Task<PagedResult<LogEventDto>?> QueryLogsAsync(LogQuery q)
    {
        var url = BuildQueryUrl("/api/logs", q);
        try { return await _http.GetFromJsonAsync<PagedResult<LogEventDto>>(url, _opts); }
        catch { return null; }
    }

    public async Task<LogEventDto?> GetLogByIdAsync(long id)
    {
        try { return await _http.GetFromJsonAsync<LogEventDto>($"/api/logs/{id}", _opts); }
        catch { return null; }
    }

    public async Task<LogStats?> GetStatsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var sb = new StringBuilder("/api/logs/stats?");
        if (from.HasValue) sb.Append($"from={Uri.EscapeDataString(from.Value.ToString("o"))}&");
        if (to.HasValue) sb.Append($"to={Uri.EscapeDataString(to.Value.ToString("o"))}&");
        try { return await _http.GetFromJsonAsync<LogStats>(sb.ToString(), _opts); }
        catch { return null; }
    }

    public async Task<List<string>?> GetApplicationsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<string>>("/api/admin/applications", _opts); }
        catch { return null; }
    }

    public async Task<List<AlertRuleDto>?> GetAlertsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<AlertRuleDto>>("/api/alerts", _opts); }
        catch { return null; }
    }

    public async Task<AlertRuleDto?> CreateAlertAsync(AlertRuleRequest request)
    {
        var resp = await _http.PostAsJsonAsync("/api/alerts", request);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AlertRuleDto>(_opts);
    }

    public async Task<AlertRuleDto?> UpdateAlertAsync(int id, AlertRuleRequest request)
    {
        var resp = await _http.PutAsJsonAsync($"/api/alerts/{id}", request);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AlertRuleDto>(_opts);
    }

    public Task DeleteAlertAsync(int id) => _http.DeleteAsync($"/api/alerts/{id}");

    public async Task<List<AlertFiredDto>?> GetAlertHistoryAsync(int id)
    {
        try { return await _http.GetFromJsonAsync<List<AlertFiredDto>>($"/api/alerts/{id}/history", _opts); }
        catch { return null; }
    }

    public async Task<TestExpressionResult?> TestExpressionAsync(string expression)
    {
        var resp = await _http.PostAsJsonAsync("/api/alerts/test", new { expression });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TestExpressionResult>(_opts);
    }

    public async Task<List<ApiKeyDto>?> GetApiKeysAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiKeyDto>>("/api/admin/apikeys", _opts); }
        catch { return null; }
    }

    public async Task<CreateApiKeyResult?> CreateApiKeyAsync(string label, string? defaultApplication)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/admin/apikeys", new { label, defaultApplication });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<CreateApiKeyResult>(_opts);
        }
        catch { return null; }
    }

    public async Task<RotateApiKeyResult?> RotateApiKeyAsync(int id)
    {
        var resp = await _http.PostAsync($"/api/admin/apikeys/{id}/rotate", null);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RotateApiKeyResult>(_opts);
    }

    public Task RevokeApiKeyAsync(int id) => _http.DeleteAsync($"/api/admin/apikeys/{id}");

    public async Task<PurgeResult?> TriggerRetentionAsync()
    {
        var resp = await _http.PostAsync("/api/admin/logs/purge/trigger", null);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PurgeResult>(_opts);
    }

    public async Task<PurgeResult?> PurgeLogsAsync(DateTimeOffset before)
    {
        var resp = await _http.DeleteAsync($"/api/admin/logs/purge?before={Uri.EscapeDataString(before.ToString("o"))}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PurgeResult>(_opts);
    }

    private static string BuildQueryUrl(string base_, LogQuery q)
    {
        var sb = new StringBuilder(base_).Append('?');
        if (q.From.HasValue) sb.Append($"from={Uri.EscapeDataString(q.From.Value.ToString("o"))}&");
        if (q.To.HasValue) sb.Append($"to={Uri.EscapeDataString(q.To.Value.ToString("o"))}&");
        if (!string.IsNullOrEmpty(q.Level)) sb.Append($"level={Uri.EscapeDataString(q.Level)}&");
        if (!string.IsNullOrEmpty(q.App)) sb.Append($"app={Uri.EscapeDataString(q.App)}&");
        if (!string.IsNullOrEmpty(q.Env)) sb.Append($"env={Uri.EscapeDataString(q.Env)}&");
        if (!string.IsNullOrEmpty(q.Q)) sb.Append($"q={Uri.EscapeDataString(q.Q)}&");
        if (!string.IsNullOrEmpty(q.TraceId)) sb.Append($"traceId={Uri.EscapeDataString(q.TraceId)}&");
        if (!string.IsNullOrEmpty(q.Prop)) sb.Append($"prop={Uri.EscapeDataString(q.Prop)}&");
        if (!string.IsNullOrEmpty(q.PropValue)) sb.Append($"propValue={Uri.EscapeDataString(q.PropValue)}&");
        sb.Append($"page={q.Page}&pageSize={q.PageSize}&desc={q.Desc}");
        return sb.ToString();
    }
}
