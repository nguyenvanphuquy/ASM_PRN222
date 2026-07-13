using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;

namespace PresentationLayer.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage(DashboardHome.PageFor(User));
        return RedirectToPage("/Auth/Login");
    }
}
