using LogVault.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Channels;
using DomainLogLevel = LogVault.Domain.Entities.LogLevel;

namespace LogVault.Application.Workers;

public class SimulationWorker : BackgroundService
{
    private readonly Channel<IReadOnlyList<LogEvent>> _channel;
    private readonly ILogger<SimulationWorker> _logger;
    private readonly Random _rng = new();

    private static readonly string[] Apps =
        ["OrderService", "AuthService", "PaymentGateway", "InventoryService", "NotificationService"];

    private static readonly string[] Envs = ["Production", "Staging"];

    private static readonly string[] Hosts =
        ["web-01", "web-02", "worker-01", "worker-02", "api-gw-01"];

    public SimulationWorker(
        Channel<IReadOnlyList<LogEvent>> channel,
        ILogger<SimulationWorker> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimulationWorker started — writing synthetic log events to ingestion channel");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int burstSize = _rng.Next(1, 11);
                int delayMs   = _rng.Next(200, 2001);

                var events = new List<LogEvent>(burstSize);
                for (int i = 0; i < burstSize; i++)
                    events.Add(BuildEvent());

                await _channel.Writer.WriteAsync(events, stoppingToken);
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ChannelClosedException)     { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SimulationWorker encountered an unexpected error");
                await Task.Delay(2000, stoppingToken).ContinueWith(_ => { });
            }
        }

        _logger.LogInformation("SimulationWorker stopped");
    }

    private LogEvent BuildEvent()
    {
        var now   = DateTimeOffset.UtcNow;
        var app   = Apps[_rng.Next(Apps.Length)];
        var env   = Envs[_rng.Next(Envs.Length)];
        var host  = Hosts[_rng.Next(Hosts.Length)];
        var level = PickLevel();

        var (template, rendered, exception, props) = BuildMessage(app, level);

        string? traceId = _rng.Next(10) < 3 ? GenerateHex(32) : null;
        string? spanId  = traceId != null    ? GenerateHex(16) : null;

        return new LogEvent
        {
            Timestamp         = now,
            IngestedAt        = now,
            Level             = level,
            MessageTemplate   = template,
            RenderedMessage   = rendered,
            Exception         = exception,
            SourceApplication = app,
            SourceEnvironment = env,
            SourceHost        = host,
            TraceId           = traceId,
            SpanId            = spanId,
            PropertiesJson    = JsonSerializer.Serialize(props),
        };
    }

    private DomainLogLevel PickLevel() => _rng.Next(100) switch
    {
        < 5  => DomainLogLevel.Verbose,
        < 20 => DomainLogLevel.Debug,
        < 70 => DomainLogLevel.Information,
        < 85 => DomainLogLevel.Warning,
        < 95 => DomainLogLevel.Error,
        _    => DomainLogLevel.Fatal
    };

    private string GenerateHex(int length)
    {
        var bytes = new byte[length / 2];
        _rng.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private (string template, string rendered, string? exception, Dictionary<string, object?> props)
        BuildMessage(string app, DomainLogLevel level) => app switch
    {
        "OrderService"        => BuildOrderMessage(level),
        "AuthService"         => BuildAuthMessage(level),
        "PaymentGateway"      => BuildPaymentMessage(level),
        "InventoryService"    => BuildInventoryMessage(level),
        "NotificationService" => BuildNotificationMessage(level),
        _                     => BuildGenericMessage(app, level)
    };

    private (string, string, string?, Dictionary<string, object?>) BuildOrderMessage(DomainLogLevel level)
    {
        int orderId = _rng.Next(10000, 99999);

        if (level <= DomainLogLevel.Information)
        {
            string status = new[] { "Created", "Processing", "Shipped", "Delivered" }[_rng.Next(4)];
            decimal amount = Math.Round((decimal)(_rng.NextDouble() * 1000), 2);
            return (
                "Order {OrderId} status changed to {Status}. Amount: {Amount:C}",
                $"Order {orderId} status changed to {status}. Amount: {amount:C}",
                null,
                new() { ["OrderId"] = orderId, ["Status"] = status, ["Amount"] = amount }
            );
        }

        if (level == DomainLogLevel.Warning)
        {
            int retries = _rng.Next(1, 5);
            return (
                "Order {OrderId} fulfillment retry {RetryCount}",
                $"Order {orderId} fulfillment retry {retries}",
                null,
                new() { ["OrderId"] = orderId, ["RetryCount"] = retries }
            );
        }

        string op = new[] { "ProcessOrder", "UpdateInventory", "SendConfirmation" }[_rng.Next(3)];
        return (
            "Failed to process order {OrderId} in {Operation}",
            $"Failed to process order {orderId} in {op}",
            $"System.InvalidOperationException: Order {orderId} is in an invalid state\r\n" +
            $"   at OrderService.OrderProcessor.{op}() in OrderProcessor.cs:line {_rng.Next(50, 300)}\r\n" +
            $"   at OrderService.OrderController.Post() in OrderController.cs:line {_rng.Next(20, 80)}",
            new() { ["OrderId"] = orderId, ["Operation"] = op }
        );
    }

    private (string, string, string?, Dictionary<string, object?>) BuildAuthMessage(DomainLogLevel level)
    {
        string user = $"user{_rng.Next(1, 100)}@corp.example.com";

        if (level <= DomainLogLevel.Information)
        {
            string action = new[] { "Login", "Logout", "TokenRefresh", "PasswordChange" }[_rng.Next(4)];
            return (
                "User {UserId} performed {Action}",
                $"User {user} performed {action}",
                null,
                new() { ["UserId"] = user, ["Action"] = action,
                        ["IpAddress"] = $"10.0.{_rng.Next(1, 10)}.{_rng.Next(1, 255)}" }
            );
        }

        int attempts = _rng.Next(3, 10);
        string? ex = level >= DomainLogLevel.Error
            ? $"AuthService.Exceptions.AccountLockedException: Account locked after {attempts} " +
              $"failed attempts for {user}\r\n" +
              $"   at AuthService.AuthManager.ValidateCredentials() in AuthManager.cs:line {_rng.Next(40, 150)}"
            : null;
        return (
            "Authentication failed for {UserId} — {FailedAttempts} attempts",
            $"Authentication failed for {user} — {attempts} attempts",
            ex,
            new() { ["UserId"] = user, ["FailedAttempts"] = attempts }
        );
    }

    private (string, string, string?, Dictionary<string, object?>) BuildPaymentMessage(DomainLogLevel level)
    {
        int txId      = _rng.Next(100000, 999999);
        decimal amount = Math.Round((decimal)(_rng.NextDouble() * 500), 2);
        string provider = new[] { "Stripe", "PayPal", "Braintree" }[_rng.Next(3)];

        if (level <= DomainLogLevel.Information)
        {
            return (
                "Payment {TransactionId} of {Amount:C} processed via {Provider}",
                $"Payment {txId} of {amount:C} processed via {provider}",
                null,
                new() { ["TransactionId"] = txId, ["Amount"] = amount, ["Provider"] = provider }
            );
        }

        string reason = new[] { "Insufficient funds", "Card declined", "Gateway timeout", "Invalid card number" }[_rng.Next(4)];
        string? ex = level >= DomainLogLevel.Error
            ? $"PaymentGateway.Exceptions.PaymentDeclinedException: {reason}\r\n" +
              $"   at PaymentGateway.GatewayClient.ChargeAsync() in GatewayClient.cs:line {_rng.Next(80, 200)}"
            : null;
        return (
            "Payment {TransactionId} failed: {Reason}",
            $"Payment {txId} failed: {reason}",
            ex,
            new() { ["TransactionId"] = txId, ["Reason"] = reason, ["Provider"] = provider }
        );
    }

    private (string, string, string?, Dictionary<string, object?>) BuildInventoryMessage(DomainLogLevel level)
    {
        string sku = $"SKU-{_rng.Next(1000, 9999)}";
        int qty    = _rng.Next(0, 500);

        if (level <= DomainLogLevel.Information)
        {
            return (
                "Inventory updated for {Sku}: quantity {Quantity}",
                $"Inventory updated for {sku}: quantity {qty}",
                null,
                new() { ["Sku"] = sku, ["Quantity"] = qty }
            );
        }

        if (level == DomainLogLevel.Warning)
        {
            return (
                "Low stock alert for {Sku}: only {Quantity} remaining",
                $"Low stock alert for {sku}: only {qty} remaining",
                null,
                new() { ["Sku"] = sku, ["Quantity"] = qty }
            );
        }

        return (
            "Stock reservation failed for {Sku} — {Quantity} requested",
            $"Stock reservation failed for {sku} — {qty} requested",
            $"InventoryService.Exceptions.InsufficientStockException: Cannot reserve {qty} units of {sku}\r\n" +
            $"   at InventoryService.ReservationManager.ReserveAsync() in ReservationManager.cs:line {_rng.Next(30, 120)}",
            new() { ["Sku"] = sku, ["Quantity"] = qty }
        );
    }

    private (string, string, string?, Dictionary<string, object?>) BuildNotificationMessage(DomainLogLevel level)
    {
        string channel   = new[] { "Email", "SMS", "Push" }[_rng.Next(3)];
        string recipient = $"user{_rng.Next(1, 100)}@example.com";

        if (level <= DomainLogLevel.Information)
        {
            return (
                "Notification sent via {Channel} to {Recipient}",
                $"Notification sent via {channel} to {recipient}",
                null,
                new() { ["Channel"] = channel, ["Recipient"] = recipient, ["TemplateId"] = _rng.Next(1, 20) }
            );
        }

        int retryMs = _rng.Next(1000, 30000);
        return (
            "Notification delivery failed via {Channel}, retrying in {RetryAfterMs}ms",
            $"Notification delivery failed via {channel}, retrying in {retryMs}ms",
            null,
            new() { ["Channel"] = channel, ["RetryAfterMs"] = retryMs, ["Recipient"] = recipient }
        );
    }

    private (string, string, string?, Dictionary<string, object?>) BuildGenericMessage(string app, DomainLogLevel level)
    {
        int idx   = _rng.Next(1, 10000);
        string? ex = level >= DomainLogLevel.Error
            ? $"System.Exception: Unexpected error in {app}\r\n" +
              $"   at {app}.Worker.RunAsync() in Worker.cs:line {_rng.Next(10, 200)}"
            : null;
        return (
            "Event {EventIndex} from {Application} at level {Level}",
            $"Event {idx} from {app} at level {level}",
            ex,
            new() { ["EventIndex"] = idx, ["Application"] = app, ["Level"] = level.ToString() }
        );
    }
}
