using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Reports;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IReportService _report;
    public IndexModel(IReportService report) => _report = report;

    public TokenReport Tokens { get; private set; } = new();
    public RevenueReport Revenue { get; private set; } = new();
    public string Range { get; private set; } = "month";

    public async Task OnGetAsync(string? range)
    {
        ViewData["Title"] = "Báo cáo & Thống kê";
        ViewData["TopbarTitle"] = "📈 Báo cáo & Thống kê";

        Range = range is "week" or "month" or "all" ? range : "month";
        Tokens = await _report.GetTokenReportAsync(Range);
        Revenue = await _report.GetRevenueReportAsync();
    }
}
