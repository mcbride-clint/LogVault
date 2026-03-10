using System.ComponentModel.DataAnnotations;

namespace LogVault.Api.Configuration;

public class RetentionOptions
{
    public const string Section = "Retention";

    public bool AutoPurgeEnabled { get; set; } = false;

    [Range(1, 3650, ErrorMessage = "Retention:RetainDays must be between 1 and 3650.")]
    public int RetainDays { get; set; } = 90;
}
