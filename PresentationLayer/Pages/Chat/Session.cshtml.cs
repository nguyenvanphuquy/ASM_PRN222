using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Chat;

[Authorize]
public class SessionModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;

    public SessionModel(IChatService chatService, ISubjectService subjectService)
    {
        _chatService = chatService;
        _subjectService = subjectService;
    }

    public ServiceLayer.DTOs.ChatSessionDto? Session { get; private set; }
    public List<ServiceLayer.DTOs.ChatMessageDto> Messages { get; private set; } = [];
    // Lịch sử trò chuyện + danh sách môn cho thanh bên (chọn môn để hỏi).
    public List<ServiceLayer.DTOs.ChatSessionDto> Sessions { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];
    public ChatAnswer? LastAnswer { get; private set; }

    [BindProperty] public string Question { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["Title"] = "Chat";
        ViewData["TopbarTitle"] = "💬 Chat AI";

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Session = await _chatService.GetSessionAsync(id);
        if (Session == null || Session.UserId != userId) return NotFound();

        Messages = await _chatService.GetMessagesAsync(id);
        await LoadSidebarAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Session = await _chatService.GetSessionAsync(id);
        if (Session == null || Session.UserId != userId) return NotFound();

        if (!string.IsNullOrWhiteSpace(Question))
        {
            try
            {
                LastAnswer = await _chatService.AskAsync(id, userId, Question.Trim());
            }
            catch
            {
                TempData["Error"] = "Lỗi khi gọi AI. Vui lòng thử lại.";
            }
        }

        Messages = await _chatService.GetMessagesAsync(id);
        await LoadSidebarAsync(userId);
        Question = "";
        return Page();
    }

    // Tạo cuộc trò chuyện mới với môn đã chọn (subjectId rỗng = Tất cả các môn).
    public async Task<IActionResult> OnPostCreateAsync(string? subjectId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var session = await _chatService.CreateSessionAsync(userId, subjectId);
        return RedirectToPage("/Chat/Session", new { id = session.Id });
    }

    private async Task LoadSidebarAsync(string userId)
    {
        Sessions = await _chatService.GetSessionsAsync(userId);
        Subjects = await _subjectService.GetAllAsync();
    }

    // Tên môn hiển thị cho một session (rỗng = tất cả các môn).
    public string SubjectLabel(string? subjectId)
        => string.IsNullOrEmpty(subjectId)
            ? "🌐 Tất cả các môn"
            : "📘 " + (Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name ?? "Môn học");
}
