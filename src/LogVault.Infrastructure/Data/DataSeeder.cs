using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogVault.Domain.Entities;
using LogVault.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainLogLevel = LogVault.Domain.Entities.LogLevel;

namespace LogVault.Infrastructure.Data;

/// <summary>
/// Seeds the database with realistic test data.
/// Invoke via: dotnet run --project src/LogVault.Api -- --seed
/// </summary>
public class DataSeeder(LogVaultDbContext db, ILogger<DataSeeder> logger)
{
    // Well-known API key for seeded data — use this value in X-Api-Key header
    public const string SeedApiKeyRaw = "lv-seed-key-abcdef1234567890abcdef1234567890";
    public const string SeedOwnerId = "CORP\\seed.user";

    private static readonly string[] Apps = ["OrderService", "PaymentService", "AuthService", "NotificationService", "ApiGateway"];
    private static readonly string[] Envs = ["Production", "Staging"];
    private static readonly string[] Hosts = ["web-01", "web-02", "worker-01", "worker-02"];

    public async Task SeedAsync(bool force = false)
    {
        if (!force && await db.LogEvents.AnyAsync())
        {
            logger.LogInformation("Database already contains data. Use --seed --force to re-seed.");
            return;
        }

        if (force)
        {
            logger.LogInformation("Clearing existing data...");
            db.AlertsFired.RemoveRange(db.AlertsFired);
            db.AlertRecipients.RemoveRange(db.AlertRecipients);
            db.AlertRules.RemoveRange(db.AlertRules);
            db.SavedFilters.RemoveRange(db.SavedFilters);
            db.DashboardWidgets.RemoveRange(db.DashboardWidgets);
            db.Dashboards.RemoveRange(db.Dashboards);
            db.ApiKeys.RemoveRange(db.ApiKeys.Where(k => k.Label == "Seed Test Key"));
            db.LogEvents.RemoveRange(db.LogEvents);
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Seeding log events...");
        await SeedLogEventsAsync();

        logger.LogInformation("Seeding alert rules...");
        await SeedAlertRulesAsync();

        logger.LogInformation("Seeding saved filters...");
        await SeedSavedFiltersAsync();

        logger.LogInformation("Seeding API key...");
        await SeedApiKeyAsync();

        logger.LogInformation("Seeding dashboard...");
        await SeedDashboardAsync();

        logger.LogInformation("Seeding complete.");
        logger.LogInformation("  Seed API Key (raw): {Key}", SeedApiKeyRaw);
        logger.LogInformation("  Use header: X-Api-Key: {Key}", SeedApiKeyRaw);
    }

    private async Task SeedLogEventsAsync()
    {
        var rng = new Random(42); // deterministic seed
        var now = DateTimeOffset.UtcNow;
        var events = new List<LogEvent>(400);

        // Spread events over the last 7 days
        for (int i = 0; i < 400; i++)
        {
            var minutesAgo = rng.NextDouble() * 60 * 24 * 7;
            var timestamp = now.AddMinutes(-minutesAgo);
            var app = Apps[rng.Next(Apps.Length)];
            var env = Envs[rng.Next(Envs.Length)];
            var host = Hosts[rng.Next(Hosts.Length)];
            var level = PickLevel(rng);

            var (template, rendered, exception, props) = BuildMessage(rng, app, level, i);

            // ~30% get trace correlation
            string? traceId = rng.Next(10) < 3 ? Guid.NewGuid().ToString("N") : null;
            string? spanId = traceId != null ? Guid.NewGuid().ToString("N")[..16] : null;

            events.Add(new LogEvent
            {
                Timestamp = timestamp,
                IngestedAt = timestamp.AddMilliseconds(rng.Next(10, 200)),
                Level = level,
                MessageTemplate = template,
                RenderedMessage = rendered,
                Exception = exception,
                SourceApplication = app,
                SourceEnvironment = env,
                SourceHost = host,
                TraceId = traceId,
                SpanId = spanId,
                PropertiesJson = JsonSerializer.Serialize(props),
            });
        }

        // Add a burst of errors in the last hour for alert testing
        for (int i = 0; i < 20; i++)
        {
            var timestamp = now.AddMinutes(-rng.NextDouble() * 60);
            events.Add(new LogEvent
            {
                Timestamp = timestamp,
                IngestedAt = timestamp.AddMilliseconds(rng.Next(10, 200)),
                Level = DomainLogLevel.Error,
                MessageTemplate = "Unhandled exception in {Operation}",
                RenderedMessage = $"Unhandled exception in ProcessOrder",
                Exception = "System.InvalidOperationException: Order state is invalid\r\n   at OrderService.OrderProcessor.ProcessOrder() in OrderProcessor.cs:line 142",
                SourceApplication = "OrderService",
                SourceEnvironment = "Production",
                SourceHost = "web-01",
                PropertiesJson = JsonSerializer.Serialize(new { Operation = "ProcessOrder", OrderId = rng.Next(10000, 99999) }),
            });
        }

        await db.LogEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();
        logger.LogInformation("  Inserted {Count} log events.", events.Count);
    }

    private async Task SeedAlertRulesAsync()
    {
        var rules = new List<AlertRule>
        {
            new()
            {
                Name = "High Error Rate",
                OwnerId = SeedOwnerId,
                FilterExpression = "",
                MinimumLevel = DomainLogLevel.Error,
                SourceApplicationFilter = null,
                ThrottleMinutes = 30,
                IsEnabled = true,
                WebhookFormat = WebhookFormat.Generic,
                Recipients = [new AlertRecipient { Email = "ops@example.com" }]
            },
            new()
            {
                Name = "Payment Failures",
                OwnerId = SeedOwnerId,
                FilterExpression = "payment",
                MinimumLevel = DomainLogLevel.Warning,
                SourceApplicationFilter = "PaymentService",
                ThrottleMinutes = 60,
                IsEnabled = true,
                WebhookFormat = WebhookFormat.Slack,
                WebhookUrl = "https://hooks.slack.example/T000/B000/XXXX",
                Recipients = [new AlertRecipient { Email = "payments@example.com" }]
            },
            new()
            {
                Name = "Fatal Events",
                OwnerId = SeedOwnerId,
                FilterExpression = "",
                MinimumLevel = DomainLogLevel.Fatal,
                SourceApplicationFilter = null,
                ThrottleMinutes = 5,
                IsEnabled = true,
                WebhookFormat = WebhookFormat.Teams,
                Recipients =
                [
                    new AlertRecipient { Email = "oncall@example.com" },
                    new AlertRecipient { Email = "manager@example.com" }
                ]
            }
        };

        await db.AlertRules.AddRangeAsync(rules);
        await db.SaveChangesAsync();
        logger.LogInformation("  Inserted {Count} alert rules.", rules.Count);
    }

    private async Task SeedSavedFiltersAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var prodErrorsQuery = new LogEventQuery(
            From: null, To: null,
            MinLevel: DomainLogLevel.Error, MaxLevel: null,
            SourceApplication: null, SourceEnvironment: "Production",
            MessageContains: null, ExceptionContains: null,
            PropertyKey: null, PropertyValue: null, TraceId: null,
            Page: 1, PageSize: 50, SortBy: "Timestamp", Descending: true);

        var authIssuesQuery = new LogEventQuery(
            From: null, To: null,
            MinLevel: DomainLogLevel.Warning, MaxLevel: null,
            SourceApplication: "AuthService", SourceEnvironment: null,
            MessageContains: null, ExceptionContains: null,
            PropertyKey: null, PropertyValue: null, TraceId: null,
            Page: 1, PageSize: 50, SortBy: "Timestamp", Descending: true);

        var todayWarningsQuery = new LogEventQuery(
            From: now.Date, To: null,
            MinLevel: DomainLogLevel.Warning, MaxLevel: DomainLogLevel.Warning,
            SourceApplication: null, SourceEnvironment: null,
            MessageContains: null, ExceptionContains: null,
            PropertyKey: null, PropertyValue: null, TraceId: null,
            Page: 1, PageSize: 50, SortBy: "Timestamp", Descending: true);

        var filters = new List<SavedFilter>
        {
            new()
            {
                Name = "Production Errors",
                OwnerId = SeedOwnerId,
                FilterJson = JsonSerializer.Serialize(prodErrorsQuery),
                IsPinned = true,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3)
            },
            new()
            {
                Name = "Auth Service Issues",
                OwnerId = SeedOwnerId,
                FilterJson = JsonSerializer.Serialize(authIssuesQuery),
                IsPinned = false,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                Name = "Today's Warnings",
                OwnerId = SeedOwnerId,
                FilterJson = JsonSerializer.Serialize(todayWarningsQuery),
                IsPinned = false,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await db.SavedFilters.AddRangeAsync(filters);
        await db.SaveChangesAsync();
        logger.LogInformation("  Inserted {Count} saved filters.", filters.Count);
    }

    private async Task SeedApiKeyAsync()
    {
        var hash = ComputeSha256(SeedApiKeyRaw);

        // Skip if already exists
        if (await db.ApiKeys.AnyAsync(k => k.KeyHash == hash))
        {
            logger.LogInformation("  Seed API key already exists, skipping.");
            return;
        }

        var key = new ApiKey
        {
            KeyHash = hash,
            Label = "Seed Test Key",
            DefaultApplication = "SeedTool",
            IsEnabled = true,
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await db.ApiKeys.AddAsync(key);
        await db.SaveChangesAsync();
        logger.LogInformation("  Inserted API key (Label: Seed Test Key).");
    }

    private async Task SeedDashboardAsync()
    {
        var dashboard = new Dashboard
        {
            Name = "Operations Overview",
            OwnerId = SeedOwnerId,
            IsDefault = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Widgets =
            [
                new DashboardWidget
                {
                    WidgetType = "ByLevel",
                    Title = "Events by Level (24h)",
                    SortOrder = 0,
                    ConfigJson = JsonSerializer.Serialize(new { hours = 24, app = (string?)null })
                },
                new DashboardWidget
                {
                    WidgetType = "EventRate",
                    Title = "Event Rate (7d)",
                    SortOrder = 1,
                    ConfigJson = JsonSerializer.Serialize(new { hours = 168, buckets = 14 })
                },
                new DashboardWidget
                {
                    WidgetType = "ErrorList",
                    Title = "Recent Errors",
                    SortOrder = 2,
                    ConfigJson = JsonSerializer.Serialize(new { hours = 24, limit = 10 })
                },
                new DashboardWidget
                {
                    WidgetType = "TopApplications",
                    Title = "Top Applications (24h)",
                    SortOrder = 3,
                    ConfigJson = JsonSerializer.Serialize(new { hours = 24, limit = 5 })
                }
            ]
        };

        await db.Dashboards.AddAsync(dashboard);
        await db.SaveChangesAsync();
        logger.LogInformation("  Inserted default dashboard with 4 widgets.");
    }

    private static DomainLogLevel PickLevel(Random rng)
    {
        // Verbose 5%, Debug 15%, Information 50%, Warning 15%, Error 10%, Fatal 5%
        return rng.Next(100) switch
        {
            < 5 => DomainLogLevel.Verbose,
            < 20 => DomainLogLevel.Debug,
            < 70 => DomainLogLevel.Information,
            < 85 => DomainLogLevel.Warning,
            < 95 => DomainLogLevel.Error,
            _ => DomainLogLevel.Fatal
        };
    }

    private static (string template, string rendered, string? exception, Dictionary<string, object?> props)
        BuildMessage(Random rng, string app, DomainLogLevel level, int index)
    {
        return (app, level) switch
        {
            ("OrderService", DomainLogLevel.Information) => BuildOrderInfo(rng),
            ("OrderService", DomainLogLevel.Warning) => BuildOrderWarning(rng),
            ("OrderService", >= DomainLogLevel.Error) => BuildOrderError(rng),
            ("PaymentService", DomainLogLevel.Information) => BuildPaymentInfo(rng),
            ("PaymentService", >= DomainLogLevel.Warning) => BuildPaymentFailure(rng, level),
            ("AuthService", DomainLogLevel.Information) => BuildAuthInfo(rng),
            ("AuthService", >= DomainLogLevel.Warning) => BuildAuthWarning(rng, level),
            ("NotificationService", DomainLogLevel.Information) => BuildNotifInfo(rng),
            ("NotificationService", >= DomainLogLevel.Warning) => BuildNotifWarning(rng),
            ("ApiGateway", DomainLogLevel.Information) => BuildGatewayInfo(rng),
            ("ApiGateway", >= DomainLogLevel.Warning) => BuildGatewayWarning(rng, level),
            _ => BuildGeneric(rng, app, level, index)
        };
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildOrderInfo(Random rng)
    {
        int orderId = rng.Next(10000, 99999);
        decimal amount = Math.Round((decimal)(rng.NextDouble() * 1000), 2);
        string status = new[] { "Created", "Processing", "Shipped", "Delivered" }[rng.Next(4)];
        return (
            "Order {OrderId} status changed to {Status}. Amount: {Amount:C}",
            $"Order {orderId} status changed to {status}. Amount: {amount:C}",
            null,
            new() { ["OrderId"] = orderId, ["Status"] = status, ["Amount"] = amount }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildOrderWarning(Random rng)
    {
        int orderId = rng.Next(10000, 99999);
        int retryCount = rng.Next(1, 5);
        return (
            "Order {OrderId} fulfillment retry {RetryCount}",
            $"Order {orderId} fulfillment retry {retryCount}",
            null,
            new() { ["OrderId"] = orderId, ["RetryCount"] = retryCount }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildOrderError(Random rng)
    {
        int orderId = rng.Next(10000, 99999);
        string op = new[] { "ProcessOrder", "UpdateInventory", "SendConfirmation" }[rng.Next(3)];
        return (
            "Failed to process order {OrderId} in {Operation}",
            $"Failed to process order {orderId} in {op}",
            $"System.InvalidOperationException: Order {orderId} is in an invalid state\r\n   at OrderService.OrderProcessor.{op}() in OrderProcessor.cs:line {rng.Next(50, 300)}",
            new() { ["OrderId"] = orderId, ["Operation"] = op }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildPaymentInfo(Random rng)
    {
        int txId = rng.Next(100000, 999999);
        decimal amount = Math.Round((decimal)(rng.NextDouble() * 500), 2);
        string provider = new[] { "Stripe", "PayPal", "Braintree" }[rng.Next(3)];
        return (
            "Payment {TransactionId} of {Amount:C} processed via {Provider}",
            $"Payment {txId} of {amount:C} processed via {provider}",
            null,
            new() { ["TransactionId"] = txId, ["Amount"] = amount, ["Provider"] = provider }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildPaymentFailure(Random rng, DomainLogLevel level)
    {
        int txId = rng.Next(100000, 999999);
        string reason = new[] { "Insufficient funds", "Card declined", "Gateway timeout", "Invalid card number" }[rng.Next(4)];
        string? ex = level >= DomainLogLevel.Error
            ? $"PaymentService.Exceptions.PaymentDeclinedException: {reason}\r\n   at PaymentService.PaymentGateway.ChargeAsync() in PaymentGateway.cs:line {rng.Next(80, 200)}"
            : null;
        return (
            "Payment {TransactionId} failed: {Reason}",
            $"Payment {txId} failed: {reason}",
            ex,
            new() { ["TransactionId"] = txId, ["Reason"] = reason }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildAuthInfo(Random rng)
    {
        string user = $"user{rng.Next(1, 100)}@corp.example.com";
        string action = new[] { "Login", "Logout", "TokenRefresh", "PasswordChange" }[rng.Next(4)];
        return (
            "User {UserId} performed {Action}",
            $"User {user} performed {action}",
            null,
            new() { ["UserId"] = user, ["Action"] = action, ["IpAddress"] = $"10.0.{rng.Next(1, 10)}.{rng.Next(1, 255)}" }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildAuthWarning(Random rng, DomainLogLevel level)
    {
        string user = $"user{rng.Next(1, 100)}@corp.example.com";
        int attempts = rng.Next(3, 10);
        string? ex = level >= DomainLogLevel.Error
            ? $"AuthService.Exceptions.AccountLockedException: Account locked after {attempts} failed attempts for {user}\r\n   at AuthService.AuthManager.ValidateCredentials() in AuthManager.cs:line {rng.Next(40, 150)}"
            : null;
        return (
            "Authentication failed for {UserId} — {FailedAttempts} attempts",
            $"Authentication failed for {user} — {attempts} attempts",
            ex,
            new() { ["UserId"] = user, ["FailedAttempts"] = attempts }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildNotifInfo(Random rng)
    {
        string channel = new[] { "Email", "SMS", "Push" }[rng.Next(3)];
        string recipient = $"user{rng.Next(1, 100)}@example.com";
        return (
            "Notification sent via {Channel} to {Recipient}",
            $"Notification sent via {channel} to {recipient}",
            null,
            new() { ["Channel"] = channel, ["Recipient"] = recipient, ["TemplateId"] = rng.Next(1, 20) }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildNotifWarning(Random rng)
    {
        string channel = new[] { "Email", "SMS", "Push" }[rng.Next(3)];
        int retryMs = rng.Next(1000, 30000);
        return (
            "Notification delivery failed via {Channel}, retrying in {RetryAfterMs}ms",
            $"Notification delivery failed via {channel}, retrying in {retryMs}ms",
            null,
            new() { ["Channel"] = channel, ["RetryAfterMs"] = retryMs }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildGatewayInfo(Random rng)
    {
        string method = new[] { "GET", "POST", "PUT", "DELETE" }[rng.Next(4)];
        string path = new[] { "/api/orders", "/api/products", "/api/users", "/api/payments" }[rng.Next(4)];
        int statusCode = new[] { 200, 201, 204 }[rng.Next(3)];
        int elapsed = rng.Next(5, 350);
        return (
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            $"HTTP {method} {path} responded {statusCode} in {elapsed}ms",
            null,
            new() { ["Method"] = method, ["Path"] = path, ["StatusCode"] = statusCode, ["ElapsedMs"] = elapsed }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildGatewayWarning(Random rng, DomainLogLevel level)
    {
        string path = new[] { "/api/orders", "/api/payments", "/api/users" }[rng.Next(3)];
        int statusCode = level >= DomainLogLevel.Error ? rng.Next(500, 503) : rng.Next(400, 404);
        int elapsed = rng.Next(2000, 30000);
        return (
            "HTTP request to {Path} failed with {StatusCode} after {ElapsedMs}ms",
            $"HTTP request to {path} failed with {statusCode} after {elapsed}ms",
            level >= DomainLogLevel.Error ? $"System.Net.Http.HttpRequestException: Response status code does not indicate success: {statusCode}\r\n   at ApiGateway.Proxy.ForwardAsync() in ReverseProxy.cs:line {rng.Next(80, 200)}" : null,
            new() { ["Path"] = path, ["StatusCode"] = statusCode, ["ElapsedMs"] = elapsed }
        );
    }

    private static (string, string, string?, Dictionary<string, object?>) BuildGeneric(Random rng, string app, DomainLogLevel level, int index)
    {
        return (
            "Event {EventIndex} from {Application} at level {Level}",
            $"Event {index} from {app} at level {level}",
            level >= DomainLogLevel.Error ? $"System.Exception: Generic error in {app}\r\n   at {app}.Worker.RunAsync() in Worker.cs:line {rng.Next(10, 200)}" : null,
            new() { ["EventIndex"] = index, ["Application"] = app, ["Level"] = level.ToString() }
        );
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
