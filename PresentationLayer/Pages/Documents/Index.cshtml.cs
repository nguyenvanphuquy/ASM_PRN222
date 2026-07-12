using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Documents;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly IUserService _userService;
    private readonly INotificationService _notifier;

    public IndexModel(
        IDocumentService docService,
        ISubjectService subjectService,
        IChapterService chapterService,
        IUserService userService,
        INotificationService notifier)
    {
        _docService = docService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _userService = userService;
        _notifier = notifier;
    }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Người dùng";

    public List<ServiceLayer.DTOs.DocumentDto> Documents { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];
    public List<ServiceLayer.DTOs.ChapterDto> Chapters { get; private set; } = [];
    public Dictionary<string, string> UserNames { get; private set; } = [];
    public Dictionary<string, string> ChapterTitles { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public string? SubjectId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ChapterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Tài liệu";
        ViewData["TopbarTitle"] = "📄 Tài liệu";

        Subjects = await _subjectService.GetAllAsync();
        Documents = await _docService.SearchAsync(SubjectId, Query, Status, ChapterId);

        if (!string.IsNullOrEmpty(SubjectId))
            Chapters = await _chapterService.GetBySubjectAsync(SubjectId);

        ChapterTitles = [];
        foreach (var sid in Documents.Select(d => d.SubjectId).Distinct())
        {
            var chapters = await _chapterService.GetBySubjectAsync(sid);
            foreach (var c in chapters)
                ChapterTitles[c.Id] = c.Title;
        }

        var users = await _userService.GetAllAsync();
        UserNames = users.ToDictionary(u => u.Id, u => string.IsNullOrEmpty(u.FullName) ? u.Username : u.FullName);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        if (role == "Admin")
        {
            TempData["Error"] = "Admin không được phép xoá tài liệu.";
            return RedirectToPage();
        }
        var canManage = role == "Lecturer" || User.HasClaim("CanUpload", "true");
        if (!canManage)
        {
            TempData["Error"] = "Bạn không có quyền thực hiện hành động này.";
            return RedirectToPage();
        }

        await _docService.DeleteAsync(id);
        TempData["Success"] = "Đã xoá tài liệu.";
        await _notifier.ActivityAsync("📄", "Tài liệu", Actor, "Xoá một tài liệu");
        return RedirectToPage();
    }
}
