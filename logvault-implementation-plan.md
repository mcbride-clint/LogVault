# LogVault — Implementation Plan
> ASP.NET Hosted Log Ingestion, Query, and Alerting Platform
> Target: Claude Code

---

## 1. Project Overview

Build **LogVault**, a self-hosted ASP.NET Core web application that ingests, stores, parses, queries, and displays structured log messages. It exposes an HTTP ingestion API compatible with Serilog's HTTP sink, accepts file-based log uploads (IIS, plain text, JSON), provides a Blazor WebAssembly front-end with Active Directory authentication, and supports user-defined email alerts triggered by custom log queries.

---

## 2. Solution Structure

```
LogVault.sln
├── src/
│   ├── LogVault.Api/                  # ASP.NET Core Web API + Blazor WASM host
│   ├── LogVault.Application/          # Business logic, use cases, alert engine
│   ├── LogVault.Domain/               # Entities, interfaces, value objects
│   ├── LogVault.Infrastructure/       # SQLite + Oracle EF Core implementations
│   ├── LogVault.Infrastructure.Mail/  # Email dispatch (SMTP)
│   └── LogVault.Client/               # Blazor WebAssembly front-end
└── tests/
    ├── LogVault.Api.Tests/
    ├── LogVault.Application.Tests/
    └── LogVault.Infrastructure.Tests/
```

---

## 3. Technology Stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core 8 (LTS) |
| Front-end | Blazor WebAssembly (.NET 8) |
| ORM | Entity Framework Core 8 |
| Default database | SQLite (Microsoft.EntityFrameworkCore.Sqlite) |
| Optional database | Oracle (Oracle.EntityFrameworkCore) |
| Authentication | Active Directory via LDAP (Novell.Directory.Ldap.NETStandard or DirectoryServices) |
| Authorization | ASP.NET Core cookie auth + Role claims from AD groups |
| Email | MailKit (SMTP) |
| Background jobs | .NET `BackgroundService` + `System.Threading.Channels` |
| Structured log parsing | Newtonsoft.Json / System.Text.Json |
| IIS log parsing | Custom parser (W3C Extended Log Format) |
| Testing | xUnit + Moq + FluentAssertions |

---

## 4. Domain Model (`LogVault.Domain`)

### 4.1 Entities

```csharp
// Core log event — maps to Serilog compact JSON format
public class LogEvent
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }           // Trace/Debug/Info/Warning/Error/Fatal
    public string MessageTemplate { get; set; }
    public string RenderedMessage { get; set; }
    public string? Exception { get; set; }
    public string? SourceApplication { get; set; }  // populated from property or header
    public string? SourceEnvironment { get; set; }
    public string? SourceHost { get; set; }
    public string PropertiesJson { get; set; }      // full properties bag as JSON
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
}

// User-defined alert rule
public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string OwnerId { get; set; }             // AD username
    public string FilterExpression { get; set; }    // see §8
    public LogLevel MinimumLevel { get; set; }
    public string? SourceApplicationFilter { get; set; }
    public int ThrottleMinutes { get; set; }        // minimum gap between alert emails
    public DateTimeOffset? LastFiredAt { get; set; }
    public bool IsEnabled { get; set; }
    public ICollection<AlertRecipient> Recipients { get; set; }
}

public class AlertRecipient
{
    public int Id { get; set; }
    public int AlertRuleId { get; set; }
    public string Email { get; set; }
}

// Fired alert history
public class AlertFired
{
    public long Id { get; set; }
    public int AlertRuleId { get; set; }
    public long TriggeringEventId { get; set; }
    public DateTimeOffset FiredAt { get; set; }
    public bool EmailSent { get; set; }
}
```

### 4.2 Enums

```csharp
public enum LogLevel { Verbose, Debug, Information, Warning, Error, Fatal }
```

### 4.3 Repository Interfaces

```csharp
public interface ILogEventRepository
{
    Task<long> InsertAsync(LogEvent logEvent, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<LogEvent> events, CancellationToken ct = default);
    Task<PagedResult<LogEvent>> QueryAsync(LogEventQuery query, CancellationToken ct = default);
    Task<LogEvent?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}

public interface IAlertRuleRepository
{
    Task<IReadOnlyList<AlertRule>> GetAllEnabledAsync(CancellationToken ct = default);
    Task<AlertRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AlertRule>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<int> UpsertAsync(AlertRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task UpdateLastFiredAsync(int id, DateTimeOffset firedAt, CancellationToken ct = default);
}

public interface IAlertFiredRepository
{
    Task RecordAsync(AlertFired fired, CancellationToken ct = default);
    Task<IReadOnlyList<AlertFired>> GetRecentAsync(int alertRuleId, int count, CancellationToken ct = default);
}
```

### 4.4 Supporting Types

```csharp
public record LogEventQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    LogLevel? MinLevel,
    LogLevel? MaxLevel,
    string? SourceApplication,
    string? SourceEnvironment,
    string? MessageContains,
    string? ExceptionContains,
    string? PropertyKey,
    string? PropertyValue,
    string? TraceId,
    int Page,
    int PageSize,
    string SortBy,           // "Timestamp" (default)
    bool Descending          // default true
);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
```

---

## 5. Infrastructure (`LogVault.Infrastructure`)

### 5.1 EF Core DbContext

```csharp
public class LogVaultDbContext : DbContext
{
    public DbSet<LogEvent> LogEvents => Set<LogEvent>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertRecipient> AlertRecipients => Set<AlertRecipient>();
    public DbSet<AlertFired> AlertsFired => Set<AlertFired>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(LogVaultDbContext).Assembly);
    }
}
```

**Entity Configurations (Fluent API):**
- `LogEvent`: composite index on `(Timestamp DESC, Level)`, index on `SourceApplication`, index on `TraceId`. Store `PropertiesJson` as TEXT.
- `AlertRule`: index on `OwnerId`, cascade delete to `AlertRecipient` and `AlertFired`.

### 5.2 SQLite Implementation

- Register: `services.AddDbContext<LogVaultDbContext>(o => o.UseSqlite(connectionString))`
- Apply migrations at startup via `db.Database.MigrateAsync()`
- Enable WAL mode for concurrent reads: `PRAGMA journal_mode=WAL;` executed on first connection
- Implement all repository interfaces using EF Core LINQ

### 5.3 Oracle Implementation (Future)

- Separate project: `LogVault.Infrastructure.Oracle`
- Register: `services.AddDbContext<LogVaultDbContext>(o => o.UseOracle(connectionString))`
- No code changes to repositories — only DI registration changes
- Migration folder: `Migrations/Oracle/`
- Configure via `appsettings.json` `"DatabaseProvider": "Oracle"` switch in `Program.cs`

### 5.4 Repository Pattern Registration

```csharp
// LogVault.Infrastructure/ServiceCollectionExtensions.cs
public static IServiceCollection AddLogVaultInfrastructure(
    this IServiceCollection services, IConfiguration config)
{
    var provider = config["DatabaseProvider"] ?? "Sqlite";
    if (provider == "Oracle")
        services.AddDbContext<LogVaultDbContext>(o => o.UseOracle(...));
    else
        services.AddDbContext<LogVaultDbContext>(o => o.UseSqlite(...));

    services.AddScoped<ILogEventRepository, EfLogEventRepository>();
    services.AddScoped<IAlertRuleRepository, EfAlertRuleRepository>();
    services.AddScoped<IAlertFiredRepository, EfAlertFiredRepository>();
    return services;
}
```

---

## 6. Log Ingestion (`LogVault.Application` + `LogVault.Api`)

### 6.1 Serilog HTTP Sink Ingestion

Serilog's `Serilog.Sinks.Http` sink POSTs batches of events as a JSON payload. Support both compact (`CLEF`) and standard formats.

**API Endpoint:**
```
POST /api/ingest/serilog
Content-Type: application/json
X-Api-Key: {key}              (optional API key header for source identification)
```

**Expected payload (Serilog HTTP sink default):**
```json
{
  "events": [
    {
      "@t": "2024-01-15T10:22:33.1234567Z",
      "@mt": "User {UserId} logged in from {IpAddress}",
      "@l": "Information",
      "@x": null,
      "UserId": 42,
      "IpAddress": "192.168.1.1",
      "Application": "MyApp"
    }
  ]
}
```

**Also support CLEF (Compact Log Event Format) — single events, one per line:**
```
POST /api/ingest/clef
```

**Ingestion service contract:**
```csharp
public interface ILogIngestionService
{
    Task<int> IngestSerilogBatchAsync(SerilogBatchPayload payload, string? sourceApp, CancellationToken ct);
    Task<int> IngestClefStreamAsync(Stream clefStream, string? sourceApp, CancellationToken ct);
    Task<int> IngestIisLogFileAsync(Stream logStream, string? sourceApp, CancellationToken ct);
    Task<int> IngestGenericFileAsync(Stream logStream, string? sourceApp, LogFileFormat format, CancellationToken ct);
}
```

### 6.2 File Ingestion via API

```
POST /api/ingest/file
Content-Type: multipart/form-data

Fields:
  file        (required) — the log file
  format      (required) — "IIS", "CLEF", "SerilogJson", "PlainText"
  application (optional) — source application name
  environment (optional) — environment tag
```

**IIS W3C Log Parser:**
- Skip comment lines starting with `#`
- Parse `#Fields:` directive to map column positions dynamically
- Map W3C fields: `date`, `time` → `Timestamp`; `s-ip`, `cs-method`, `cs-uri-stem`, `cs-uri-query`, `sc-status`, `time-taken`, etc. → `PropertiesJson`
- Set `Level` based on HTTP status: 5xx → Error, 4xx → Warning, else → Information
- Set `RenderedMessage` to a human-readable HTTP request summary

### 6.3 Ingestion Pipeline (Background Channel)

To decouple HTTP response time from database writes:

```csharp
// Channel-based pipeline
services.AddSingleton<Channel<IReadOnlyList<LogEvent>>>(
    Channel.CreateBounded<IReadOnlyList<LogEvent>>(new BoundedChannelOptions(1000)));
services.AddHostedService<LogIngestionWorker>();  // drains channel, bulk-inserts to DB
```

- HTTP endpoint writes parsed events to channel and returns `202 Accepted` immediately
- `LogIngestionWorker` drains in batches of up to 500 every 500ms
- If channel is full, fall back to synchronous insert (do not drop events)

### 6.4 Alert Evaluation (Background Service)

```csharp
public class AlertEvaluationWorker : BackgroundService
{
    // Triggered after each batch is committed to DB
    // For each enabled AlertRule, evaluate the newest events against the rule's filter
    // If match found and throttle allows: fire email, record AlertFired
}
```

---

## 7. API Layer (`LogVault.Api`)

### 7.1 Controllers / Minimal API Endpoints

```
Ingestion
  POST   /api/ingest/serilog          Serilog HTTP sink batch
  POST   /api/ingest/clef             CLEF stream
  POST   /api/ingest/file             Multipart file upload

Query
  GET    /api/logs                    Paginated query (query params map to LogEventQuery)
  GET    /api/logs/{id}               Single event by ID
  GET    /api/logs/stats              Counts grouped by level/hour (dashboard charts)

Alerts
  GET    /api/alerts                  Current user's alert rules
  POST   /api/alerts                  Create alert rule
  PUT    /api/alerts/{id}             Update alert rule
  DELETE /api/alerts/{id}             Delete alert rule
  GET    /api/alerts/{id}/history     Alert fired history

Administration
  GET    /api/admin/applications      Distinct source applications seen
  DELETE /api/admin/logs/purge        Purge logs older than given date (admin role)
  GET    /api/admin/ingestion/stats   Events/sec, queue depth

Authentication
  POST   /api/auth/login              LDAP credential validation, issues cookie
  POST   /api/auth/logout
  GET    /api/auth/me                 Returns current user + roles
```

### 7.2 API Key Authentication for Ingestion Endpoints

Ingestion endpoints support both:
1. Cookie auth (for in-browser testing / Swagger)
2. `X-Api-Key` header (for Serilog sinks running on servers)

```csharp
// ApiKey entity stored in DB, hashed
public class ApiKey
{
    public int Id { get; set; }
    public string KeyHash { get; set; }      // SHA-256 of raw key
    public string Label { get; set; }
    public string DefaultApplication { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Admin UI allows generating and revoking API keys.

### 7.3 OpenAPI / Swagger

- Include Swagger UI at `/swagger` in Development
- Document all request/response types
- Include sample payloads for ingestion endpoints

---

## 8. Query & Filter System

### 8.1 Query Parameters (GET /api/logs)

| Parameter | Type | Description |
|---|---|---|
| `from` | ISO 8601 | Start timestamp |
| `to` | ISO 8601 | End timestamp |
| `level` | string | Minimum level (Verbose/Debug/Information/Warning/Error/Fatal) |
| `maxLevel` | string | Maximum level |
| `app` | string | Source application (exact or partial) |
| `env` | string | Environment |
| `q` | string | Full-text search on RenderedMessage + Exception |
| `prop` | string | Property key filter (e.g. `UserId`) |
| `propValue` | string | Property value filter |
| `traceId` | string | Distributed trace ID |
| `page` | int | Page number (1-based, default 1) |
| `pageSize` | int | Page size (default 50, max 500) |
| `sort` | string | Field to sort (default `Timestamp`) |
| `desc` | bool | Descending order (default true) |

### 8.2 Alert Filter Expression Language

Alert rules use a simple expression string evaluated against each `LogEvent`:

```
Syntax examples:
  level >= Error
  app == "PaymentService" AND level >= Warning
  message contains "timeout"
  exception contains "SqlException"
  prop:UserId == "42"
  level == Fatal OR (level == Error AND app == "AuthService")
```

**Implementation:** Parse expression into a predicate tree at rule-save time. Compile to `Func<LogEvent, bool>` using a simple recursive descent parser. Store compiled delegate in memory; reload on startup. Validate expression on save and return error if invalid.

---

## 9. Authentication & Authorization

### 9.1 Active Directory / LDAP Authentication

```csharp
public interface IAdAuthService
{
    Task<AdAuthResult> AuthenticateAsync(string username, string password);
    Task<IReadOnlyList<string>> GetGroupsAsync(string username);
}

public record AdAuthResult(bool Success, string? DisplayName, string? Email, string? ErrorMessage);
```

**Configuration (`appsettings.json`):**
```json
"ActiveDirectory": {
  "Server": "ldap.corp.example.com",
  "Port": 389,
  "UseSsl": false,
  "SearchBase": "DC=corp,DC=example,DC=com",
  "Domain": "CORP",
  "AdminGroup": "LogVault-Admins",
  "UserGroup": "LogVault-Users"
}
```

**Cookie auth flow:**
1. Client POSTs credentials to `/api/auth/login`
2. Server validates via LDAP bind
3. On success, retrieves AD group membership
4. Issues ASP.NET Core cookie with claims: `Name`, `Email`, `Role` (mapped from AD groups)
5. Blazor WASM uses `AuthenticationStateProvider` backed by `/api/auth/me`

### 9.2 Roles

| Role | AD Group | Permissions |
|---|---|---|
| `Admin` | `LogVault-Admins` | All operations, purge logs, manage API keys |
| `User` | `LogVault-Users` | Query logs, manage own alert rules |
| `Ingest` | API Key | POST to ingestion endpoints only |

### 9.3 Authorization Policies

```csharp
services.AddAuthorization(o =>
{
    o.AddPolicy("RequireUser", p => p.RequireRole("User", "Admin"));
    o.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
    o.AddPolicy("CanIngest", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") ||
        ctx.User.IsInRole("User") ||
        ctx.User.HasClaim("auth_method", "ApiKey")));
});
```

---

## 10. Blazor WebAssembly Front-End (`LogVault.Client`)

### 10.1 Pages & Components

```
Pages/
  Login.razor                   Username + password form
  Dashboard.razor                Summary charts, recent errors, alerts fired
  Logs/
    Index.razor                 Log explorer (filter bar + results table)
    Detail.razor                Single event detail with properties
  Alerts/
    Index.razor                 List user's alert rules
    Edit.razor                  Create / edit alert rule form
  Admin/
    ApiKeys.razor               Generate / revoke API keys (Admin only)
    Purge.razor                 Log retention management (Admin only)

Shared/
  LogLevelBadge.razor           Color-coded level pill
  FilterBar.razor               Reusable filter controls
  LogTable.razor                Virtualized log results grid
  PropertyViewer.razor          Formatted JSON property viewer
  PaginationBar.razor
  ConfirmDialog.razor
```

### 10.2 Log Explorer (Core UI Feature)

**Filter bar fields:**
- Date/time range picker (From / To)
- Level multi-select (checkboxes: Verbose, Debug, Information, Warning, Error, Fatal)
- Application dropdown (populated from `/api/admin/applications`)
- Environment dropdown
- Full-text search input
- Property key + value inputs (for structured property filtering)
- Trace ID input
- "Live tail" toggle (polls every 5 seconds for new events)

**Results table columns:**
- Timestamp (local time, sortable)
- Level (color-coded badge)
- Application
- Message (truncated, expand on click)
- Exception indicator icon

**Event detail panel (slide-out or modal):**
- Full rendered message
- Exception with stack trace (monospace, scrollable)
- Properties displayed as key-value pairs (formatted JSON values expandable)
- Trace/Span ID with copy button
- Raw JSON toggle

### 10.3 Dashboard

- **Level distribution chart** (bar chart by level, last 24h)
- **Events over time chart** (line chart, last 24h, hourly buckets)
- **Top error messages** (grouped by MessageTemplate, count, last seen)
- **Recent fatal/error events** (live-updating list, last 20)
- **Alert rules summary** (count enabled, last fired)

### 10.4 Alert Rule Editor

Form fields:
- Rule name
- Minimum log level (dropdown)
- Source application filter (optional)
- Filter expression (text area with syntax hint)
- **Test expression button** — sends expression to `/api/alerts/test` which evaluates against recent events and returns sample matches
- Throttle interval (minutes between emails)
- Recipient email addresses (add/remove list)
- Enable/disable toggle

### 10.5 HTTP Client Configuration

```csharp
// Program.cs (Client)
builder.Services.AddHttpClient("LogVaultApi", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});
builder.Services.AddScoped<ILogVaultApiClient, LogVaultApiClient>();
```

Use a typed `ILogVaultApiClient` service wrapping `HttpClient` — never call `HttpClient` directly from components.

---

## 11. Email Alerting (`LogVault.Infrastructure.Mail`)

```csharp
public interface IAlertEmailService
{
    Task SendAlertAsync(AlertRule rule, LogEvent triggeringEvent, CancellationToken ct);
}
```

**Email template (HTML):**
- Subject: `[LogVault Alert] {RuleName} — {Level} in {Application}`
- Body includes: rule name, triggered event timestamp, level, message, exception (if any), top 10 properties, link to event in LogVault UI
- Plain-text fallback

**Configuration:**
```json
"Email": {
  "SmtpHost": "smtp.corp.example.com",
  "SmtpPort": 25,
  "UseSsl": false,
  "FromAddress": "logvault@corp.example.com",
  "FromName": "LogVault Alerts"
}
```

Use **MailKit** (`MimeKit` + `MailKit`) for SMTP dispatch.

---

## 12. Configuration Reference (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=logvault.db"
  },
  "DatabaseProvider": "Sqlite",
  "ActiveDirectory": {
    "Server": "",
    "Port": 389,
    "UseSsl": false,
    "SearchBase": "DC=corp,DC=example,DC=com",
    "Domain": "CORP",
    "AdminGroup": "LogVault-Admins",
    "UserGroup": "LogVault-Users"
  },
  "Email": {
    "SmtpHost": "",
    "SmtpPort": 25,
    "UseSsl": false,
    "FromAddress": "logvault@corp.example.com",
    "FromName": "LogVault Alerts"
  },
  "Ingestion": {
    "MaxBatchSize": 500,
    "ChannelCapacity": 1000,
    "FlushIntervalMs": 500
  },
  "Retention": {
    "AutoPurgeEnabled": false,
    "RetainDays": 90
  },
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
```

---

## 13. Project Setup Commands (for Claude Code)

```bash
# Create solution
dotnet new sln -n LogVault

# Projects
dotnet new webapi  -n LogVault.Api           -o src/LogVault.Api
dotnet new classlib -n LogVault.Application  -o src/LogVault.Application
dotnet new classlib -n LogVault.Domain       -o src/LogVault.Domain
dotnet new classlib -n LogVault.Infrastructure -o src/LogVault.Infrastructure
dotnet new classlib -n LogVault.Infrastructure.Mail -o src/LogVault.Infrastructure.Mail
dotnet new blazorwasm -n LogVault.Client     -o src/LogVault.Client

# Test projects
dotnet new xunit -n LogVault.Api.Tests            -o tests/LogVault.Api.Tests
dotnet new xunit -n LogVault.Application.Tests    -o tests/LogVault.Application.Tests
dotnet new xunit -n LogVault.Infrastructure.Tests -o tests/LogVault.Infrastructure.Tests

# Add to solution
dotnet sln add src/**/*.csproj tests/**/*.csproj

# Key NuGet packages — LogVault.Infrastructure
dotnet add src/LogVault.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/LogVault.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/LogVault.Infrastructure package Novell.Directory.Ldap.NETStandard

# Key NuGet packages — LogVault.Infrastructure.Mail
dotnet add src/LogVault.Infrastructure.Mail package MailKit

# Key NuGet packages — LogVault.Api
dotnet add src/LogVault.Api package Microsoft.AspNetCore.Authentication.Cookies
dotnet add src/LogVault.Api package Swashbuckle.AspNetCore

# Key NuGet packages — LogVault.Client
dotnet add src/LogVault.Client package Microsoft.AspNetCore.Components.WebAssembly.Authentication
```

---

## 14. IIS Hosting Notes

- Publish `LogVault.Api` as self-contained or framework-dependent
- Set `ASPNETCORE_ENVIRONMENT` in `web.config`
- Enable WebSocket support in IIS if live-tail via SignalR is added later
- Blazor WASM static files are served by the API host via `app.UseBlazorFrameworkFiles()` + `app.UseStaticFiles()`
- Ensure app pool runs under an identity with LDAP read access

---

## 15. Additional Recommendations

### 15.1 Distributed Trace Correlation
Extract `TraceId` and `SpanId` from incoming `traceparent` headers (W3C Trace Context) on ingestion endpoints and store alongside the log event. This allows correlating log events across services by trace ID.

### 15.2 SignalR Live Tail (Optional Enhancement)
Add a SignalR hub (`/hubs/logs`) that pushes newly ingested events to connected Blazor clients matching their current filter. This replaces the polling approach for the live tail feature.

### 15.3 API Key Rotation
Admin UI should support generating a new key for a given label (rotating it) without downtime — mark the old key as expiring in 24h while the new key is active.

### 15.4 Log Retention Background Job
A nightly `IHostedService` that checks `Retention:RetainDays` and calls `ILogEventRepository.DeleteOlderThanAsync`. Can also be triggered manually from the Admin UI.

### 15.5 Source Application Auto-Detection
When no `Application` property is present in a log event, fall back to:
1. `X-Application-Name` request header (set by Serilog sink configuration)
2. The API key's `DefaultApplication` field
3. Literal `"Unknown"`

### 15.6 Export Feature
Add `GET /api/logs/export?format=json|csv` with the same filter parameters as the query endpoint, streamed as a file download. Useful for handing logs off to another team.

### 15.7 Health Checks
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<LogVaultDbContext>()
    .AddSmtpHealthCheck(...)   // custom, attempts SMTP connection
    .AddLdapHealthCheck(...);  // custom, attempts LDAP bind
app.MapHealthChecks("/health");
```

---

## 16. Implementation Order (Suggested for Claude Code)

1. **Domain** — entities, interfaces, value objects, `LogEventQuery`, `PagedResult<T>`
2. **Infrastructure** — `LogVaultDbContext`, EF configurations, migrations, SQLite repository implementations
3. **Application** — `ILogIngestionService` and its implementation, Serilog/CLEF parsers, IIS log parser
4. **Api** — ingestion endpoints, query endpoints, auth endpoints, AD auth service
5. **Infrastructure.Mail** — `IAlertEmailService` implementation
6. **Application** (alert engine) — `AlertEvaluationWorker`, filter expression parser/evaluator
7. **Client (Blazor WASM)** — authentication, log explorer, dashboard, alert editor
8. **Tests** — unit tests for parsers, filter expression evaluator, repository mock tests
9. **Polish** — Swagger docs, health checks, `appsettings` validation on startup, logging of LogVault's own internal errors to Console sink

---

*End of Plan*
