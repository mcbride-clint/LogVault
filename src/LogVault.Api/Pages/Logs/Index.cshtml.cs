using LogVault.Application.Parsing;
using LogVault.Domain.Models;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DomainLogLevel = LogVault.Domain.Entities.LogLevel;

namespace LogVault.Api.Pages.Logs;

public class IndexModel(ILogEventRepository repo, ISavedFilterRepository savedFilters) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Level { get; set; }
    [BindProperty(SupportsGet = true)] public string? App { get; set; }
    [BindProperty(SupportsGet = true)] public string? Env { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? TraceId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Fts { get; set; }
    [BindProperty(SupportsGet = true)] public string? Expr { get; set; }
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 50;
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "Timestamp";
    [BindProperty(SupportsGet = true)] public bool Desc { get; set; } = true;

    public PagedResult<LogVault.Domain.Entities.LogEvent> Result { get; private set; } = new([], 0, 1, 50);
    public IReadOnlyList<LogVault.Domain.Entities.SavedFilter> SavedFilters { get; private set; } = [];
    public string? ExprError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var ownerId = User.Identity?.Name ?? "anonymous";
        SavedFilters = await savedFilters.GetByOwnerAsync(ownerId, ct);

        var size = Math.Min(PageSize, 500);
        DomainLogLevel? minLevel = Enum.TryParse<DomainLogLevel>(Level, true, out var l) ? l : null;

        ParsedQueryExpression? parsed = null;
        if (!string.IsNullOrWhiteSpace(Expr))
        {
            parsed = LogQueryExpressionParser.Parse(Expr);
            if (parsed.HasError)
            {
                ExprError = parsed.Error;
                return;
            }
        }

        var query = new LogEventQuery(
            From: parsed?.From ?? From,
            To: parsed?.To ?? To,
            MinLevel: parsed?.MinLevel ?? minLevel,
            MaxLevel: parsed?.MaxLevel,
            SourceApplication: parsed?.SourceApplication ?? App,
            SourceEnvironment: parsed?.SourceEnvironment ?? Env,
            MessageContains: parsed?.MessageContains ?? Q,
            ExceptionContains: parsed?.ExceptionContains,
            PropertyKey: null, PropertyValue: null,
            TraceId: parsed?.TraceId ?? TraceId,
            Page: Page, PageSize: size,
            SortBy: Sort, Descending: Desc,
            FullTextSearch: Fts,
            PropertyConditions: parsed?.PropertyConditions.Count > 0 ? parsed.PropertyConditions : null);

        Result = await repo.QueryAsync(query, ct);
    }

    public Dictionary<string, string?> BuildPageRouteValues(int page)
    {
        var d = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = PageSize.ToString(),
            ["sort"] = Sort,
            ["desc"] = Desc.ToString().ToLower(),
        };
        if (From.HasValue) d["from"] = From.Value.ToString("O");
        if (To.HasValue) d["to"] = To.Value.ToString("O");
        if (!string.IsNullOrEmpty(Level)) d["level"] = Level;
        if (!string.IsNullOrEmpty(App)) d["app"] = App;
        if (!string.IsNullOrEmpty(Env)) d["env"] = Env;
        if (!string.IsNullOrEmpty(Q)) d["q"] = Q;
        if (!string.IsNullOrEmpty(TraceId)) d["traceId"] = TraceId;
        if (!string.IsNullOrEmpty(Fts)) d["fts"] = Fts;
        if (!string.IsNullOrEmpty(Expr)) d["expr"] = Expr;
        return d;
    }

    public static string LevelBadgeClass(DomainLogLevel level) => level switch
    {
        DomainLogLevel.Fatal => "lv-badge-fatal",
        DomainLogLevel.Error => "lv-badge-error",
        DomainLogLevel.Warning => "lv-badge-warning",
        DomainLogLevel.Information => "lv-badge-information",
        DomainLogLevel.Debug => "lv-badge-debug",
        _ => "lv-badge-verbose"
    };
}
