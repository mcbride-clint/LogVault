namespace LogVault.Api.Configuration;

public class ActiveDirectoryOptions
{
    public const string Section = "ActiveDirectory";

    public string AdminGroup { get; set; } = "LogVault-Admins";
    public string UserGroup { get; set; } = "LogVault-Users";

    /// <summary>
    /// When true, any authenticated Windows user is granted Admin+User roles without
    /// checking AD group membership. Intended for local development only.
    /// </summary>
    public bool GrantAllAuthenticatedUsers { get; set; } = false;
}
