using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Dashboard;

[Authorize(Policy = "LecturerOrAdmin")]
public class ExperimentsModel : PageModel
{
    private readonly IExperimentService _experiments;

    public ExperimentsModel(IExperimentService experiments) => _experiments = experiments;

    public ExperimentDashboardDto Data { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard RBL";
        ViewData["TopbarTitle"] = "📊 Kết quả thực nghiệm RBL";
        Data = await _experiments.GetDashboardAsync();
    }
}
