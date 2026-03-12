namespace LogVault.Domain.Entities;

public class Dashboard
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<DashboardWidget> Widgets { get; set; } = [];
}
