using System.ComponentModel.DataAnnotations;

namespace LogVault.Api.Configuration;

public class ActiveDirectoryOptions
{
    public const string Section = "ActiveDirectory";

    public string Server { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 389;

    public string SearchBase { get; set; } = "";
    public string Domain { get; set; } = "";
    public string AdminGroup { get; set; } = "LogVault-Admins";
    public string UserGroup { get; set; } = "LogVault-Users";
}
