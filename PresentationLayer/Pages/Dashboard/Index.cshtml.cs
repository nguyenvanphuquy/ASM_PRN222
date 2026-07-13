using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;

namespace PresentationLayer.Pages.Dashboard;

/// <summary>Chuyển hướng tới dashboard riêng theo vai trò.</summary>
[Authorize]
public class IndexModel : PageModel
{
    public IActionResult OnGet()
        => RedirectToPage(DashboardHome.PageFor(User));
}
