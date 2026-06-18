using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Pages.Feedback;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IFeedbackService _feedbackService;
    public IndexModel(IFeedbackService feedbackService) => _feedbackService = feedbackService;

    public List<DataAccessLayer.Entities.Feedback> Items { get; private set; } = [];
    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public int Rating { get; set; } = 5;
    [BindProperty] public string? DocumentId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Phản hồi";
        ViewData["TopbarTitle"] = "💡 Phản hồi";
        Items = await _feedbackService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            var fullName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";
            var avatar = User.FindFirst("AvatarPath")?.Value;
            await _feedbackService.CreateAsync(userId, fullName, avatar, Rating, Comment);
            TempData["Success"] = "Đã gửi phản hồi!";
        }
        return RedirectToPage();
    }
}
