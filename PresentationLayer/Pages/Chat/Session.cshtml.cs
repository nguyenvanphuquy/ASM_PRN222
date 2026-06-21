using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Pages.Chat;

[Authorize]
public class SessionModel : PageModel
{
    private readonly IChatService _chatService;

    public SessionModel(IChatService chatService) => _chatService = chatService;

    public ServiceLayer.DTOs.ChatSessionDto? Session { get; private set; }
    public List<ServiceLayer.DTOs.ChatMessageDto> Messages { get; private set; } = [];
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
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Session = await _chatService.GetSessionAsync(id);
        if (Session == null || Session.UserId != userId) return NotFound();

        if (string.IsNullOrWhiteSpace(Question))
        {
            Messages = await _chatService.GetMessagesAsync(id);
            return Page();
        }

        try
        {
            LastAnswer = await _chatService.AskAsync(id, userId, Question.Trim());
        }
        catch
        {
            TempData["Error"] = "Lỗi khi gọi AI. Vui lòng thử lại.";
        }

        Messages = await _chatService.GetMessagesAsync(id);
        Question = "";
        return Page();
    }
}


