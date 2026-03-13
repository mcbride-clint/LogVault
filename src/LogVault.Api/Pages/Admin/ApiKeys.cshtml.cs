using LogVault.Api.Middleware;
using LogVault.Domain.Entities;
using LogVault.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class ApiKeysModel(IApiKeyRepository keyRepo) : PageModel
{
    [BindProperty] public string NewLabel { get; set; } = "";
    [BindProperty] public string? NewDefaultApp { get; set; }

    public IReadOnlyList<ApiKey> Keys { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Keys = await keyRepo.GetAllAsync(ct);

        // If a new key was just created, show its raw value once
        if (TempData.TryGetValue("NewRawKey", out var raw) && raw is string rawKey)
            ViewData["NewRawKey"] = rawKey;
        if (TempData.TryGetValue("NewKeyLabel", out var lbl))
            ViewData["NewKeyLabel"] = lbl;
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NewLabel))
        {
            TempData["Error"] = "Label is required.";
            return RedirectToPage();
        }

        var rawKey = ApiKeyMiddleware.GenerateRawKey();
        var keyHash = ApiKeyMiddleware.ComputeSha256(rawKey);

        await keyRepo.CreateAsync(new ApiKey
        {
            KeyHash = keyHash,
            Label = NewLabel.Trim(),
            DefaultApplication = NewDefaultApp?.Trim(),
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        TempData["NewRawKey"] = rawKey;
        TempData["NewKeyLabel"] = NewLabel;
        return RedirectToPage();
    }
}
