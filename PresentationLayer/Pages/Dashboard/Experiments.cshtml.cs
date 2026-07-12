using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Dashboard;

[Authorize(Policy = "LecturerOrAdmin")]
public class ExperimentsModel : PageModel
{
    private readonly IExperimentService _experiments;
    private readonly IRblSuiteService _suite;

    public ExperimentsModel(IExperimentService experiments, IRblSuiteService suite)
    {
        _experiments = experiments;
        _suite = suite;
    }

    public ExperimentDashboardDto Data { get; private set; } = new();
    public RblSuiteResult? SuiteResult { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Kind { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard RBL";
        ViewData["TopbarTitle"] = "📊 Kết quả thực nghiệm RBL";
        Data = await _experiments.GetDashboardAsync(filterKind: NormalizeKind(Kind));
    }

    public async Task<IActionResult> OnPostRunSuiteAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        SuiteResult = await _suite.RunStandardSuiteAsync(userId);
        Data = await _experiments.GetDashboardAsync(filterKind: NormalizeKind(Kind));
        ViewData["Title"] = "Dashboard RBL";
        ViewData["TopbarTitle"] = "📊 Kết quả thực nghiệm RBL";
        return Page();
    }

    private static string? NormalizeKind(string? kind)
        => kind switch
        {
            "chunking" or "embedding" or "rag-vs-ft" => kind,
            _ => null
        };
}
