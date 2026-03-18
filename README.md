# LogVault

A self-hosted log ingestion, query, and alerting platform built on ASP.NET Core / .NET 10. LogVault accepts structured logs from your applications, stores them in SQLite, and provides a server-rendered Razor Pages UI for querying and real-time monitoring.

## Features

- **Multiple ingestion formats** — Serilog JSON, CLEF (Compact Log Event Format), IIS W3C logs, plain text, and OTLP/HTTP (OpenTelemetry)
- **Real-time log tail** — Live streaming via SignalR with per-client filters
- **Full-text search** — Single search box that matches across message, exception, and all structured properties (multi-term AND logic)
- **Structured property filtering** — Filter by property key/value with Contains, Equals, or Not Equals operators
- **Query expression syntax** — SQL-like `expr` filter combining level, app, message, timestamp, exception, trace ID, and custom property conditions (e.g. `level >= Warning AND app == "MyApi" AND prop:UserId == "42"`). Syntax highlighting and autocomplete are available in both the Log Explorer and Alert rule editor.
- **Saved Queries** — Save named queries as Private (personal) or Shared (visible to all users). Access them from the collapsible sidebar in the Log Explorer, or manage them on the dedicated Saved Queries page (`/queries`). Click any saved query to instantly apply it. Admins see all queries on the management page.
- **Create Alert from Log Explorer** — Click "Create Alert" while browsing logs to jump directly to the alert rule editor pre-filled with your current expression and application filter.
- **Alerting** — Rule-based alerts with custom filter expressions, throttling, email notifications, and webhook delivery (Generic, Slack, Teams). The filter expression editor provides the same syntax highlighting and autocomplete as the Log Explorer.
- **Log correlation view** — For any trace ID, see the full trace waterfall plus before/after context events from the same applications
- **Export** — Download query results as CSV or JSON
- **Dashboards** — Configurable widget-based dashboards (log volume, top applications, recent errors, etc.)
- **IIS live tail** — Watch IIS W3C log files in real-time (local paths via `FileSystemWatcher`, remote servers via UNC share polling). Per-source application/environment labels. Runtime enable/disable per source via Admin UI without restarting.
- **API Key auth** — Programmatic ingestion without user credentials
- **Active Directory integration** — LDAP-backed login with group-based Admin/User roles
- **Retention** — Automatic or manual purge of old log events
- **Health checks** — Built-in probes for database, SMTP, and LDAP

---

## Project Structure

```
src/
  LogVault.Domain/             # Entities, interfaces — no external dependencies
  LogVault.Infrastructure/     # EF Core + SQLite, LDAP auth, health checks, webhooks
  LogVault.Infrastructure.Mail/# MailKit SMTP for alert emails
  LogVault.Application/        # Parsers, ingestion worker, retention, alert engine
  LogVault.Api/                # ASP.NET Core minimal API, SignalR hub, Razor Pages UI

tests/
  LogVault.Application.Tests/
  LogVault.Infrastructure.Tests/
  LogVault.Api.Tests/
```

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- An LDAP/Active Directory server (or configure a stub for development)
- Optional: SMTP server for alert emails

### Run Locally

```bash
git clone <repo>
cd LogVault/src/LogVault.Api
dotnet run
```

The app starts on `http://localhost:5041` by default (HTTPS on `https://localhost:7154`). The Razor Pages UI is served from the same host. The browser opens automatically when running the `http` or `https` launch profile.

The SQLite database (`logvault.db`) is created and migrated automatically on first run.

---

## Configuration

All configuration lives in `appsettings.json` (and environment-specific overrides). Settings are validated on startup — the app will refuse to start if constraints are violated.

### Connection String

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=logvault.db"
  }
}
```

Set to any valid SQLite connection string. For IIS deployments use an absolute path, e.g. `Data Source=C:\LogVault\logvault.db`.

---

### Active Directory (`ActiveDirectory`)

Controls LDAP authentication and role-group mapping.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Server` | string | `""` | LDAP server hostname or IP |
| `Port` | int | `389` | LDAP port (1–65535) |
| `SearchBase` | string | `""` | DN to search for users, e.g. `DC=corp,DC=example,DC=com` |
| `Domain` | string | `""` | NetBIOS domain prefix, e.g. `CORP` |
| `AdminGroup` | string | `LogVault-Admins` | AD group whose members receive the Admin role |
| `UserGroup` | string | `LogVault-Users` | AD group whose members receive the User role |

```json
"ActiveDirectory": {
  "Server": "dc.corp.example.com",
  "Port": 389,
  "SearchBase": "DC=corp,DC=example,DC=com",
  "Domain": "CORP",
  "AdminGroup": "LogVault-Admins",
  "UserGroup": "LogVault-Users"
}
```

Users in `AdminGroup` receive both the `Admin` and `User` roles. Users in `UserGroup` only receive `User`.

---

### Email (`Email`)

Used to send alert notification emails via SMTP.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SmtpHost` | string | `""` | SMTP server hostname. Leave empty to disable email |
| `SmtpPort` | int | `25` | SMTP port (1–65535) |
| `UseSsl` | bool | `false` | Enable TLS/SSL |
| `Username` | string? | `null` | SMTP username (optional) |
| `Password` | string? | `null` | SMTP password (optional) |
| `FromAddress` | string | `logvault@localhost` | Sender email address |
| `FromName` | string | `LogVault Alerts` | Sender display name |

```json
"Email": {
  "SmtpHost": "smtp.corp.example.com",
  "SmtpPort": 587,
  "UseSsl": true,
  "Username": "logvault@corp.example.com",
  "Password": "secret",
  "FromAddress": "logvault@corp.example.com",
  "FromName": "LogVault Alerts"
}
```

---

### Retention (`Retention`)

Controls automatic purging of old log events.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AutoPurgeEnabled` | bool | `false` | Enable scheduled background purging |
| `RetainDays` | int | `90` | Number of days to retain events (1–3650) |

```json
"Retention": {
  "AutoPurgeEnabled": true,
  "RetainDays": 90
}
```

Admins can also trigger an immediate purge via `POST /api/admin/logs/purge/trigger` or purge before a specific date via `DELETE /api/admin/logs/purge?before={datetime}`.

---

### Ingestion (`Ingestion`)

Tunes the internal ingestion pipeline throughput.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MaxBatchSize` | int | `500` | Maximum events per database batch insert (1–10000) |
| `FlushIntervalMs` | int | `500` | How often the worker flushes the channel in milliseconds (50–60000) |
| `ChannelCapacity` | int | `1000` | Bounded channel buffer size; backpressure applied when full (100–100000) |

```json
"Ingestion": {
  "MaxBatchSize": 500,
  "FlushIntervalMs": 500,
  "ChannelCapacity": 1000
}
```

---

### API Keys (`ApiKeys`)

Controls key rotation behaviour.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `RotationGraceHours` | int | `24` | Hours the old key remains valid after rotation before expiring (1–720) |

```json
"ApiKeys": {
  "RotationGraceHours": 24
}
```

---

### IIS Watcher (`IisWatcher`)

Watches IIS W3C log directories in real-time and feeds new entries into the ingestion pipeline as they are written. Local paths use `FileSystemWatcher`; UNC/network paths (`\\server\share\...`) use polling. Only lines appended after LogVault starts are ingested — existing content and old daily files are skipped.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | bool | `false` | Master switch. Set to `true` to activate the watcher. |
| `PollIntervalMs` | int | `2000` | Default poll interval in ms for UNC paths (used when a source has no per-source override) |
| `Sources` | array | `[]` | List of `IisLogSource` objects (see below) |

Each entry in `Sources`:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Path` | string | `""` | Local directory or UNC path to watch |
| `SourceApplication` | string | `"IIS"` | Label applied to ingested events |
| `SourceEnvironment` | string? | `null` | Environment label (optional) |
| `Enabled` | bool | `true` | Whether this source is active on startup |
| `PollIntervalMs` | int? | `null` | Override poll interval for this source only (UNC paths) |

```json
"IisWatcher": {
  "Enabled": true,
  "PollIntervalMs": 2000,
  "Sources": [
    {
      "Path": "C:\\inetpub\\logs\\LogFiles\\W3SVC1",
      "SourceApplication": "IIS-local",
      "SourceEnvironment": "Development"
    },
    {
      "Path": "\\\\web-server-01\\iislogs\\W3SVC1",
      "SourceApplication": "IIS-web01",
      "SourceEnvironment": "Production",
      "PollIntervalMs": 5000
    },
    {
      "Path": "\\\\web-server-02\\iislogs\\W3SVC1",
      "SourceApplication": "IIS-web02",
      "SourceEnvironment": "Production",
      "Enabled": false
    }
  ]
}
```

Individual sources can be temporarily paused or resumed at runtime via the **Admin › IIS Watcher** page without restarting. Runtime state resets to config defaults on service restart.

---

### SignalR

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SignalR:LiveTailMaxEventsPerBroadcast` | int | `100` | Maximum events pushed to each connected client per broadcast cycle |

---

### Health Checks

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `HealthChecks:LdapProbeTimeoutSeconds` | int | `5` | LDAP connectivity probe timeout |
| `HealthChecks:SmtpProbeTimeoutSeconds` | int | `5` | SMTP connectivity probe timeout |

The health endpoint is available at `GET /health`.

---

## API Reference

### Authentication

**Login** — `POST /api/auth/login`
```json
{ "username": "jdoe", "password": "secret", "rememberMe": false }
```

**Logout** — `POST /api/auth/logout`

**Current user** — `GET /api/auth/me`

### Ingestion

All ingestion endpoints accept either a valid cookie session or an `X-Api-Key` header.

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/ingest/serilog` | JSON array of Serilog event objects |
| `POST` | `/api/ingest/clef` | Newline-delimited CLEF stream |
| `POST` | `/api/ingest/file` | Multipart file upload |
| `POST` | `/v1/logs` | OTLP/HTTP JSON (OpenTelemetry) |

File upload query parameters: `format` (IIS \| Clef \| SerilogJson \| PlainText), `application`, `environment`.

Serilog and CLEF endpoints return `202 Accepted` with an event count. The OTLP endpoint returns `200 OK` with an empty JSON body per the OTLP spec. Binary protobuf (`application/x-protobuf`) returns `415 Unsupported Media Type`.

**Example — Serilog sink:**
```json
POST /api/ingest/serilog
X-Api-Key: lv_xxxxxxxxxxxxxxxx

[
  {
    "@t": "2026-03-11T10:00:00Z",
    "@l": "Information",
    "@mt": "User {UserId} logged in",
    "@m": "User 42 logged in",
    "UserId": 42
  }
]
```

**Example — OTLP/HTTP (OpenTelemetry):**

Configure your .NET app's OpenTelemetry exporter to target `http://your-logvault-host/v1/logs` with content type `application/json`. The endpoint accepts the standard OTLP JSON payload and maps `service.name` to `SourceApplication`, severity to log level, and trace/span IDs to the correlation fields.

```json
POST /v1/logs
Content-Type: application/json
X-Api-Key: lv_xxxxxxxxxxxxxxxx

{
  "resourceLogs": [{
    "resource": {
      "attributes": [{ "key": "service.name", "value": { "stringValue": "MyApi" } }]
    },
    "scopeLogs": [{
      "logRecords": [{
        "timeUnixNano": "1741694400000000000",
        "severityNumber": 9,
        "severityText": "INFO",
        "body": { "stringValue": "Request completed in 42ms" },
        "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
        "spanId": "00f067aa0ba902b7"
      }]
    }]
  }]
}
```

### Log Query

Requires `User` or `Admin` role.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/logs` | Paginated query |
| `GET` | `/api/logs/{id}` | Single event by ID |
| `GET` | `/api/logs/trace/{traceId}` | All events for a trace ID |
| `GET` | `/api/logs/correlate` | Trace events plus surrounding context |
| `GET` | `/api/logs/stats` | Level and hourly breakdown |
| `GET` | `/api/logs/stats/top-applications` | Top N applications by event count |
| `GET` | `/api/logs/export` | Download CSV or JSON |

**Query parameters for `/api/logs`:**

| Parameter | Description |
|-----------|-------------|
| `from` / `to` | ISO 8601 datetime range |
| `level` | Minimum level (Verbose, Debug, Information, Warning, Error, Fatal) |
| `maxLevel` | Maximum level |
| `app` | Source application filter (substring match) |
| `env` | Source environment filter |
| `q` | Message text search |
| `fts` | Full-text search across message, exception, and properties (space-separated terms are ANDed) |
| `traceId` | Filter by trace ID (exact match) |
| `prop` / `propValue` | Property key/value filter |
| `propOp` | Property filter operator: `Contains` (default), `Equals`, `NotEquals` |
| `expr` | SQL-like expression filter — overrides individual params when set (e.g. `level >= Warning AND app == "MyApi" AND prop:UserId == "42"`) |
| `page` / `pageSize` | Pagination (max pageSize: 500) |
| `sort` / `desc` | Sort field and direction (default: Timestamp descending) |

**Expression syntax for `expr`:**

Conditions are separated by `AND`. Supported fields and operators:

| Field | Aliases | Operators |
|-------|---------|-----------|
| `level` | | `>=`, `>`, `<=`, `<`, `==` |
| `app` | `application` | `==`, `contains` |
| `env` | `environment` | `==` |
| `message` | `msg` | `contains`, `==` |
| `exception` | `ex` | `contains`, `==` |
| `trace` | `traceid` | `==` |
| `timestamp` | `time`, `ts` | `>=`, `>`, `<=`, `<` |
| `prop:Key` | | `==`, `!=`, `contains` |

String values may be quoted (`"my value"`) or unquoted for single words. Level values: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`.

Examples:
```
level >= Warning
level >= Error AND app == "PaymentService"
message contains "timeout" AND prop:UserId == "42"
timestamp >= "2026-01-01T00:00:00Z" AND level == Error
```

**Query parameters for `/api/logs/correlate`:**

| Parameter | Description |
|-----------|-------------|
| `traceId` | Required. Trace ID to correlate |
| `contextMinutes` | Minutes of context to include before/after the trace window (default: 5) |

Returns `{ traceId, traceEvents, contextBefore, contextAfter }`.

### Saved Filters

Requires `User` or `Admin` role.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/savedfilters` | List saved filters |
| `POST` | `/api/savedfilters` | Create a saved filter |
| `PUT` | `/api/savedfilters/{id}` | Update a saved filter |
| `DELETE` | `/api/savedfilters/{id}` | Delete a saved filter |
| `PATCH` | `/api/savedfilters/{id}/pin` | Toggle pinned state |

Pinned filters appear as one-click chips at the top of the filter panel in the UI.

### Alerts

Requires `User` or `Admin` role.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/alerts` | List rules |
| `POST` | `/api/alerts` | Create rule |
| `GET` | `/api/alerts/{id}` | Get rule |
| `PUT` | `/api/alerts/{id}` | Update rule |
| `DELETE` | `/api/alerts/{id}` | Delete rule |
| `GET` | `/api/alerts/{id}/history` | Last 50 fired events |
| `POST` | `/api/alerts/test` | Validate a filter expression |

**Alert rule body:**
```json
{
  "name": "Production Errors",
  "filterExpression": "Level == Error AND App == MyApi",
  "minimumLevel": "Error",
  "sourceApplicationFilter": "MyApi",
  "throttleMinutes": 60,
  "isEnabled": true,
  "recipientEmails": ["ops@corp.example.com"],
  "webhookUrl": "https://hooks.slack.com/services/...",
  "webhookFormat": "Slack"
}
```

`webhookFormat` accepts `Generic`, `Slack`, or `Teams`. Generic sends a flat JSON object; Slack sends an `attachments` payload; Teams sends a MessageCard.

### Admin

Requires `Admin` role.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/applications` | List distinct source application names |
| `DELETE` | `/api/admin/logs/purge?before={datetime}` | Purge events before a date |
| `POST` | `/api/admin/logs/purge/trigger` | Run retention worker immediately |
| `GET` | `/api/admin/apikeys` | List all API keys |
| `POST` | `/api/admin/apikeys` | Create a new API key |
| `POST` | `/api/admin/apikeys/{id}/rotate` | Rotate a key |
| `DELETE` | `/api/admin/apikeys/{id}` | Revoke a key |
| `POST` | `/api/admin/iiswatcher/toggle` | Enable or disable an IIS Watcher source at runtime |

**Toggle IIS Watcher source body:**
```json
{ "path": "\\\\web-server-01\\iislogs\\W3SVC1", "enabled": false }
```

**Create API key body:**
```json
{ "label": "MyApp Production", "defaultApplication": "MyApi" }
```

The raw key is returned only once on creation. Store it securely.

---

## Live Tail (SignalR)

Connect to the hub at `/hubs/logs` with a valid user session, then call `SetFilter` to subscribe:

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/logs")
  .build();

connection.on("NewEvents", events => console.log(events));

await connection.start();
await connection.invoke("SetFilter", {
  minLevel: "Warning",
  sourceApplication: "MyApi",
  messageContains: "exception"
});
```

---

## IIS Deployment

1. Publish the application:
   ```bash
   dotnet publish src/LogVault.Api -c Release -o ./publish
   ```

2. Copy the `publish` folder to your IIS site root.

3. Create an IIS Application Pool with:
   - .NET CLR Version: **No Managed Code**
   - Pipeline Mode: **Integrated**

4. Grant the Application Pool identity:
   - Read + Execute on the site folder
   - Read + Write on the database file and its parent directory

5. Set the `ASPNETCORE_ENVIRONMENT` environment variable on the Application Pool (or in `web.config`) if you want to use environment-specific `appsettings` overrides.

6. Update `appsettings.json` with an absolute path for the SQLite database and your AD / SMTP settings.

The `web.config` in the project uses out-of-process hosting (Kestrel behind IIS) and includes security response headers.

---

## Running Tests

```bash
dotnet test
```

The integration test suite (`LogVault.Api.Tests`) uses a file-based temporary SQLite database and mocks Active Directory. No external services are required.

---

## License

MIT
