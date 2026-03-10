namespace LogVault.Domain.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string FilterExpression { get; set; } = "";
    public LogLevel MinimumLevel { get; set; }
    public string? SourceApplicationFilter { get; set; }
    public int ThrottleMinutes { get; set; } = 60;
    public DateTimeOffset? LastFiredAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public ICollection<AlertRecipient> Recipients { get; set; } = new List<AlertRecipient>();
}
