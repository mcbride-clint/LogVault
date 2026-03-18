namespace LogVault.Domain.Entities;

public class SavedFilter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string FilterJson { get; set; } = "{}";
    public bool IsPinned { get; set; } = false;
    public bool IsPublic { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
