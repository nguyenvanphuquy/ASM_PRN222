using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public IndexModel(IDashboardService dashboard) => _dashboard = dashboard;

    public DashboardStats Stats { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["TopbarTitle"] = "Dashboard";
        Stats = await _dashboard.GetStatsAsync();
    }
}
