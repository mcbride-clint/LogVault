namespace LogVault.Domain.Entities;

public class DashboardWidget
{
    public int Id { get; set; }
    public int DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    /// <summary>One of: ByLevel | EventRate | ErrorList | TopApplications</summary>
    public string WidgetType { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    /// <summary>Type-specific config JSON, e.g. {"hours":24,"app":null}</summary>
    public string ConfigJson { get; set; } = "{}";
}
