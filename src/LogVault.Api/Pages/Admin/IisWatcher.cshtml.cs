using LogVault.Api.Configuration;
using LogVault.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LogVault.Api.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class IisWatcherModel(
    IOptions<IisWatcherOptions> options,
    IisWatcherRuntimeState runtimeState) : PageModel
{
    public IisWatcherOptions Options { get; private set; } = default!;
    public Dictionary<string, bool> RuntimeStates { get; private set; } = [];

    public void OnGet()
    {
        Options = options.Value;
        RuntimeStates = Options.Sources.ToDictionary(
            s => s.Path,
            s => runtimeState.IsEnabled(s.Path, s.Enabled),
            StringComparer.OrdinalIgnoreCase);
    }
}
