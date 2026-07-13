using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.DTOs;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Dashboard;

[Authorize(Roles = "Student")]
public class StudentModel : PageModel
{
    private readonly ISubjectService _subjects;
    private readonly IChatService _chat;
    private readonly IBillingService _billing;
    private readonly IFeedbackService _feedback;

    public StudentModel(
        ISubjectService subjects,
        IChatService chat,
        IBillingService billing,
        IFeedbackService feedback)
    {
        _subjects = subjects;
        _chat = chat;
        _billing = billing;
        _feedback = feedback;
    }

    public int SubjectCount { get; private set; }
    public int SessionCount { get; private set; }
    public TokenBalance Balance { get; private set; } = new(0, 0, 0, null);
    public int MyFeedbackCount { get; private set; }
    public List<ChatSessionDto> RecentSessions { get; private set; } = new();
    public List<SubjectDto> Subjects { get; private set; } = new();

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Dashboard Sinh viên";
        ViewData["TopbarTitle"] = "🏠 Dashboard Sinh viên";

        await _billing.EnsureFreeGrantAsync(UserId);

        Subjects = await _subjects.GetAllAsync();
        SubjectCount = Subjects.Count;

        var sessions = await _chat.GetSessionsAsync(UserId);
        SessionCount = sessions.Count;
        RecentSessions = sessions.Take(5).ToList();

        Balance = await _billing.GetBalanceAsync(UserId);
        MyFeedbackCount = (await _feedback.GetByUserAsync(UserId)).Count;
    }
}
