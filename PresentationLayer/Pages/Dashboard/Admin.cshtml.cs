using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Dashboard;

[Authorize(Roles = "Admin")]
public class AdminModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public AdminModel(IDashboardService dashboard) => _dashboard = dashboard;

    public DashboardStats Stats { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard Admin";
        ViewData["TopbarTitle"] = "🏠 Dashboard Admin";
        Stats = await _dashboard.GetStatsAsync();
    }
}
