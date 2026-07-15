using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;
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
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu.";
            return RedirectToPage();
        }

        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
        {
            TempData["Error"] = "Bạn chỉ được quản lý tài liệu của môn được giao.";
            return RedirectToPage();
        }

        await _docService.DeleteAsync(id);
        TempData["Success"] = $"Đã xoá tài liệu \"{doc.Title}\".";
        await _notifier.ActivityAsync("🗑", "Tài liệu", Actor, $"Xoá tài liệu \"{doc.Title}\"");
        return RedirectToPage();
    }
}
