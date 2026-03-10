namespace LogVault.Domain.Entities;

public class ApiKey
{
    public int Id { get; set; }
    public string KeyHash { get; set; } = "";
    public string Label { get; set; } = "";
    public string? DefaultApplication { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int? RotatedFromId { get; set; }
    public ApiKey? RotatedFrom { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
    public bool IsUsable => IsEnabled && !IsRevoked && !IsExpired;
}
