using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Feedback;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IFeedbackService _feedbackService;
    public IndexModel(IFeedbackService feedbackService) => _feedbackService = feedbackService;

    public List<DataAccessLayer.Entities.Feedback> Items { get; private set; } = [];
    // Phản hồi (reply) của admin, gom theo FeedbackId.
    public Dictionary<string, List<DataAccessLayer.Entities.FeedbackReply>> Replies { get; private set; } = new();
    public bool IsAdmin { get; private set; }
    public int TotalCount { get; private set; }
    public double AverageRating { get; private set; }

    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public int Rating { get; set; } = 5;

    // Dùng cho admin trả lời.
    [BindProperty] public string? ReplyContent { get; set; }
    [BindProperty] public string? FeedbackId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Phản hồi";
        ViewData["TopbarTitle"] = "💡 Phản hồi";

        IsAdmin = User.IsInRole("Admin");
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        // Chỉ Admin thấy toàn bộ feedback; người dùng khác chỉ thấy feedback của chính mình.
        Items = IsAdmin
            ? await _feedbackService.GetAllAsync()
            : await _feedbackService.GetByUserAsync(userId);

        if (Items.Count > 0)
        {
            var replies = await _feedbackService.GetRepliesForAsync(Items.Select(f => f.Id));
            Replies = replies
                .GroupBy(r => r.FeedbackId)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.CreatedAt).ToList());
        }

        if (IsAdmin)
        {
            var (total, avg) = await _feedbackService.GetStatsAsync();
            TotalCount = total;
            AverageRating = avg;
        }
    }

    // Gửi phản hồi mới — chỉ người dùng (Student/Lecturer), Admin không gửi.
    public async Task<IActionResult> OnPostAsync()
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Info"] = "Quản trị viên không gửi phản hồi.";
            return RedirectToPage();
        }

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

    // Admin trả lời một phản hồi.
    public async Task<IActionResult> OnPostReplyAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (!string.IsNullOrWhiteSpace(ReplyContent) && !string.IsNullOrWhiteSpace(FeedbackId))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var fullName = User.FindFirst("FullName")?.Value ?? "Quản trị viên";
            var avatar = User.FindFirst("AvatarPath")?.Value;
            await _feedbackService.AddReplyAsync(FeedbackId, userId, fullName, avatar, ReplyContent, true);
            TempData["Success"] = "Đã gửi phản hồi tới người dùng.";
        }
        return RedirectToPage();
    }

    // Admin xoá một phản hồi.
    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (!string.IsNullOrWhiteSpace(id))
        {
            await _feedbackService.DeleteAsync(id);
            TempData["Success"] = "Đã xoá phản hồi.";
        }
        return RedirectToPage();
    }
}
