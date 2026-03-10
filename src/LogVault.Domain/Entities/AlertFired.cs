namespace LogVault.Domain.Entities;

public class AlertFired
{
    public long Id { get; set; }
    public int AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
    public long TriggeringEventId { get; set; }
    public DateTimeOffset FiredAt { get; set; }
    public bool EmailSent { get; set; }
}
