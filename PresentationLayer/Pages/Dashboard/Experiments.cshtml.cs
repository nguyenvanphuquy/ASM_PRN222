using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;
using System.Text;

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
        Data = await _experiments.GetDashboardAsync(recentTake: 500, filterKind: NormalizeKind(Kind));
    }

    public async Task<IActionResult> OnPostRunSuiteAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        SuiteResult = await _suite.RunStandardSuiteAsync(userId);
        Data = await _experiments.GetDashboardAsync(recentTake: 500, filterKind: NormalizeKind(Kind));
        ViewData["Title"] = "Dashboard RBL";
        ViewData["TopbarTitle"] = "📊 Kết quả thực nghiệm RBL";
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var csv = await _experiments.ExportCsvAsync(200, NormalizeKind(Kind));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var name = $"RBL_Experiments_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", name);
    }

    public IActionResult OnGetExportRagas()
    {
        var path = FindRagasXlsx();
        if (path == null)
            return NotFound("Chưa có file RAGAS_Results.xlsx. Chạy: py -3 eval/ragas_benchmark.py");

        return PhysicalFile(path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "RAGAS_Results.xlsx");
    }

    private static string? FindRagasXlsx()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "eval", "RAGAS_Results.xlsx");
            if (System.IO.File.Exists(p)) return p;
        }
        var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval", "RAGAS_Results.xlsx"));
        return System.IO.File.Exists(fromBase) ? fromBase : null;
    }

    private static string? NormalizeKind(string? kind)
        => kind switch
        {
            "chunking" or "embedding" or "rag-vs-ft" => kind,
            _ => null
        };
}
