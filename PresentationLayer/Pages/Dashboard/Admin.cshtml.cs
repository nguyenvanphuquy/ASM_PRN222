using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Handler JSON để trang tự cập nhật số liệu real-time (qua SignalR) mà không cần reload.
    /// URL: /Dashboard/Admin?handler=Stats
    /// </summary>
    public async Task<IActionResult> OnGetStatsAsync()
    {
        var s = await _dashboard.GetStatsAsync();
        return new JsonResult(new
        {
            usersTotal = s.UsersTotal,
            admins = s.Admins,
            lecturers = s.Lecturers,
            students = s.Students,
            documents = s.Documents,
            chatMessages = s.ChatMessages,
            subjects = s.Subjects,
            feedbackAverage = s.FeedbackAverage,
            chatSessions = s.ChatSessions,
            chunks = s.Chunks,
            feedbackAwaiting = s.FeedbackAwaiting,
            recentDocuments = s.RecentDocuments.Select(d => new
            {
                title = d.Title,
                fileName = d.FileName,
                fileSize = d.FileSize,
                status = d.Status
            })
        });
    }
}
