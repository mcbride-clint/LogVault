using System.ComponentModel.DataAnnotations;

namespace LogVault.Api.Configuration;

public class ApiKeyOptions
{
    public const string Section = "ApiKeys";

    [Range(1, 720, ErrorMessage = "ApiKeys:RotationGraceHours must be between 1 and 720.")]
    public int RotationGraceHours { get; set; } = 24;
}
