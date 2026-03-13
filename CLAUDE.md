# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests (~60 tests across 3 projects)
dotnet test

# Run a single test project
dotnet test tests/LogVault.Application.Tests/LogVault.Application.Tests.csproj
dotnet test tests/LogVault.Infrastructure.Tests/LogVault.Infrastructure.Tests.csproj
dotnet test tests/LogVault.Api.Tests/LogVault.Api.Tests.csproj

# Run a specific test by name filter
dotnet test --filter "FullyQualifiedName~ParseSerilogPayload"

# Run the API (from solution root)
dotnet run --project src/LogVault.Api

# Seed the database
dotnet run --project src/LogVault.Api -- --seed
dotnet run --project src/LogVault.Api -- --seed --force
```

## Architecture

**Target Framework:** .NET 10 / ASP.NET Core 10

**Project layers** (inner → outer dependency direction):

| Project | Purpose |
|---|---|
| `LogVault.Domain` | Entities, repository interfaces, domain service interfaces — zero external deps |
| `LogVault.Infrastructure` | EF Core + SQLite, AD/LDAP auth, API key middleware, webhook client, health checks |
| `LogVault.Infrastructure.Mail` | MailKit SMTP implementation of `IAlertEmailService` |
| `LogVault.Application` | Parsers, `LogIngestionWorker`, `RetentionWorker`, `IAlertEvaluationService`, `ILogExportService` |
| `LogVault.Api` | Minimal API endpoints, SignalR hub, Blazor WASM hosting, `Program.cs` |
| `LogVault.Client` | Blazor WASM frontend |

## Key Technical Decisions

### SQLite + DateTimeOffset
`LogVaultDbContext.OnModelCreating` applies `DateTimeOffsetToBinaryConverter` to **all** `DateTimeOffset` properties. This is required — without it SQLite stores text and `ORDER BY` on timestamps breaks.

### Ingestion Pipeline
Inbound log events are pushed onto a `System.Threading.Channels.Channel<T>` (bounded, wait-on-full). `LogIngestionWorker` is the sole consumer, batching events into SQLite. Do not bypass the channel.

### Parser Key Matching
`SerilogPayloadParser` uses **exact-case** key matching. CLEF keys (`@t`, `@l`, `@mt`, `@m`) and PascalCase (`Timestamp`, `Level`, etc.) are both supported. `PostAsJsonAsync` sends camelCase — use CLEF keys (`@t`, `@l`, etc.) in tests.

### Authentication
- Production: Windows/Negotiate auth with claims transformation mapping AD groups to `Admin`/`User` roles.
- `CanIngest` policy also accepts `ApiKey` auth method (via `X-Api-Key` header, SHA-256 hashed).
- Cookie auth: `OnRedirectToLogin`/`OnRedirectToAccessDenied` return 401/403 for `/api` paths instead of redirecting.

### Configuration Validation
All config sections have Options classes in `src/LogVault.Api/Configuration/` registered with `ValidateDataAnnotations().ValidateOnStart()`. The SMTP/LDAP host fields intentionally have no `[Required]` to allow dev startup with empty `appsettings.json`.

## Integration Test Pattern

`LogVaultApiFactory` (in `tests/LogVault.Api.Tests/`) extends `WebApplicationFactory<Program>`:

- Uses a **file-based temp SQLite DB** (`Path.GetTempPath() + Guid + ".db"`) — never in-memory. Background workers open new scopes/connections; in-memory SQLite cannot be shared across them.
- Replaces `Negotiate` auth with `TestAuthHandler` that auto-authenticates as both `Admin` and `User` roles.
- Replaces SMTP/LDAP health checks with `NoOpHealthCheck`.
- `InitializeAsync` calls `GET /health` then waits 1 000 ms to let `LogIngestionWorker` fully start before tests run.
- `CreateAdminClientAsync()` — returns an authenticated HTTP client.
- `CreateApiKeyClientAsync(label)` — creates a raw key, stores its SHA-256 hash, returns a client with the `X-Api-Key` header set.
