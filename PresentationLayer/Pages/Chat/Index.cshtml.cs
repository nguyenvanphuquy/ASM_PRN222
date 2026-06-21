using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Pages.Chat;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IChatService chatService, ISubjectService subjectService)
    {
        _chatService = chatService;
        _subjectService = subjectService;
    }

    public List<ServiceLayer.DTOs.ChatSessionDto> Sessions { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Chat AI";
        ViewData["TopbarTitle"] = "💬 Chat AI";

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Sessions = await _chatService.GetSessionsAsync(userId);
        Subjects = await _subjectService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string? subjectId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var session = await _chatService.CreateSessionAsync(userId, subjectId);
        return RedirectToPage("/Chat/Session", new { id = session.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _chatService.DeleteSessionAsync(id);
        TempData["Info"] = "Đã xoá cuộc hội thoại.";
        return RedirectToPage();
    }
}


