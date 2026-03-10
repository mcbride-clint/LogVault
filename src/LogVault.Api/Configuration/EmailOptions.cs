using System.ComponentModel.DataAnnotations;

namespace LogVault.Api.Configuration;

public class EmailOptions
{
    public const string Section = "Email";

    public string SmtpHost { get; set; } = "";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 25;

    public bool UseSsl { get; set; } = false;

    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "logvault@localhost";

    public string FromName { get; set; } = "LogVault Alerts";
}
