namespace LogVault.Domain.Entities;

public class AlertRecipient
{
    public int Id { get; set; }
    public int AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
    public string Email { get; set; } = "";
}
