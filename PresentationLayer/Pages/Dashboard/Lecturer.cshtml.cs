using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Dashboard;

[Authorize(Roles = "Lecturer")]
public class LecturerModel : PageModel
{
    private readonly IDashboardService _dashboard;
    private readonly IExperimentService _experiments;

    public LecturerModel(IDashboardService dashboard, IExperimentService experiments)
    {
        _dashboard = dashboard;
        _experiments = experiments;
    }

    public DashboardStats Stats { get; private set; } = new();
    public ExperimentDashboardDto Experiments { get; private set; } = new();
    public List<Document> MyDocuments { get; private set; } = new();
    public int AssignedSubjectCount { get; private set; }
    public bool CanUpload { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard Giảng viên";
        ViewData["TopbarTitle"] = "🏠 Dashboard Giảng viên";

        Stats = await _dashboard.GetStatsAsync();
        Experiments = await _experiments.GetDashboardAsync(recentTake: 5);

        var assigned = (User.FindFirst("AssignedSubjects")?.Value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AssignedSubjectCount = assigned.Count;
        CanUpload = SubjectDocumentAuth.HasAssignedSubjects(User)
                    && User.HasClaim("CanUpload", "true");

        MyDocuments = assigned.Count == 0
            ? Stats.RecentDocuments.Take(5).ToList()
            : Stats.RecentDocuments
                .Where(d => assigned.Contains(d.SubjectId))
                .Take(5)
                .ToList();

        // Nếu chưa có doc thuộc môn được giao, vẫn hiện vài tài liệu gần đây để GV theo dõi.
        if (MyDocuments.Count == 0)
            MyDocuments = Stats.RecentDocuments.Take(5).ToList();
    }
}
