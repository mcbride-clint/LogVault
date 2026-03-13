using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LogVault.Api.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Dashboard");
}
