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
    private readonly INotificationService _notifier;

    public IndexModel(IFeedbackService feedbackService, INotificationService notifier)
    {
        _feedbackService = feedbackService;
        _notifier = notifier;
    }

    public List<DataAccessLayer.Entities.Feedback> Items { get; private set; } = [];
    public Dictionary<string, List<DataAccessLayer.Entities.FeedbackReply>> Replies { get; private set; } = new();
    public bool IsAdmin { get; private set; }
    public int TotalCount { get; private set; }
    public double AverageRating { get; private set; }

    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public int Rating { get; set; } = 5;
    [BindProperty] public string? ReplyContent { get; set; }
    [BindProperty] public string? FeedbackId { get; set; }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "User";

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Phản hồi";
        ViewData["TopbarTitle"] = "💡 Phản hồi";

        IsAdmin = User.IsInRole("Admin");
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

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
            var fb = await _feedbackService.CreateAsync(userId, fullName, avatar, Rating, Comment);
            var preview = Comment.Length > 80 ? Comment[..80] + "…" : Comment;

            await _notifier.FeedbackChangedAsync("created", fb.Id, userId, preview);
            await _notifier.NotifyRoleAsync("Admin", "info", "Phản hồi mới",
                $"{fullName} vừa gửi phản hồi ({Rating}★).");
            await _notifier.ActivityAsync("💡", "Phản hồi", Actor, $"Gửi phản hồi mới ({Rating}★)");

            TempData["Success"] = "Đã gửi phản hồi!";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReplyAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (!string.IsNullOrWhiteSpace(ReplyContent) && !string.IsNullOrWhiteSpace(FeedbackId))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var fullName = User.FindFirst("FullName")?.Value ?? "Quản trị viên";
            var avatar = User.FindFirst("AvatarPath")?.Value;

            var all = await _feedbackService.GetAllAsync();
            var fb = all.FirstOrDefault(f => f.Id == FeedbackId);

            await _feedbackService.AddReplyAsync(FeedbackId, userId, fullName, avatar, ReplyContent, true);

            if (fb != null)
            {
                await _notifier.SendAsync(fb.UserId, "info", "Admin đã trả lời phản hồi",
                    ReplyContent.Length > 100 ? ReplyContent[..100] + "…" : ReplyContent);
                await _notifier.FeedbackChangedAsync("reply", fb.Id, fb.UserId, ReplyContent);
            }

            await _notifier.ActivityAsync("↩️", "Phản hồi", Actor, "Trả lời một phản hồi của người dùng");
            TempData["Success"] = "Đã gửi phản hồi tới người dùng.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (!string.IsNullOrWhiteSpace(id))
        {
            await _feedbackService.DeleteAsync(id);
            await _notifier.FeedbackChangedAsync("deleted", id);
            await _notifier.ActivityAsync("🗑", "Phản hồi", Actor, "Xoá một phản hồi");
            TempData["Success"] = "Đã xoá phản hồi.";
        }
        return RedirectToPage();
    }
}
