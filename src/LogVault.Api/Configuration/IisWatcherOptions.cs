namespace LogVault.Api.Configuration;

public class IisWatcherOptions
{
    public const string Section = "IisWatcher";

    public bool Enabled { get; set; } = false;
    public int PollIntervalMs { get; set; } = 2000;
    public IisLogSource[] Sources { get; set; } = [];
}

public class IisLogSource
{
    public string Path { get; set; } = "";
    public string SourceApplication { get; set; } = "IIS";
    public string? SourceEnvironment { get; set; }
    public bool Enabled { get; set; } = true;
    public int? PollIntervalMs { get; set; }
}
