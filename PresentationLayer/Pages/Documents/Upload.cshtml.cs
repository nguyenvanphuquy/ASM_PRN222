using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Documents;

[Authorize(Policy = "CanUploadDocuments")]
public class UploadModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly INotificationService _notifier;

    public UploadModel(IDocumentService docService, ISubjectService subjectService, IChapterService chapterService, INotificationService notifier)
    {
        _docService = docService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _notifier = notifier;
    }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Người dùng";

    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];
    public List<Chapter> Chapters { get; private set; } = [];

    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public string SubjectId { get; set; } = "";
    [BindProperty] public string? ChapterId { get; set; }
    [BindProperty] public string? Title { get; set; }

    // Danh sách id các môn mà giảng viên được giao (từ claim "AssignedSubjects").
    private List<string> AssignedSubjectIds() =>
        (User.FindFirst("AssignedSubjects")?.Value ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Upload tài liệu";
        ViewData["TopbarTitle"] = "⬆️ Upload tài liệu";

        var assigned = AssignedSubjectIds();

        // Người upload chỉ được thao tác trên các môn được admin giao (admin không vào được trang này).
        Subjects = (await _subjectService.GetAllAsync())
            .Where(s => assigned.Contains(s.Id))
            .ToList();

        // Nếu chỉ có đúng một môn thì chọn sẵn cho tiện.
        if (Subjects.Count == 1) SubjectId = Subjects[0].Id;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "Upload tài liệu";

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var assigned = AssignedSubjectIds();

        // Admin không được upload; chỉ giảng viên được giao môn mới upload (và đúng môn được giao).
        if (role == "Admin")
        {
            return Forbid();
        }

        if (assigned.Count == 0)
        {
            Subjects = [];
            ModelState.AddModelError("", "Bạn chưa được phân công môn học nào nên không thể upload.");
            return Page();
        }

        Subjects = (await _subjectService.GetAllAsync())
            .Where(s => assigned.Contains(s.Id))
            .ToList();

        if (UploadFile == null || UploadFile.Length == 0)
        {
            ModelState.AddModelError("File", "Vui lòng chọn file.");
            return Page();
        }

        // Chặn upload cho môn không được giao.
        if (string.IsNullOrEmpty(SubjectId) || !assigned.Contains(SubjectId))
        {
            ModelState.AddModelError("SubjectId", "Vui lòng chọn một môn học bạn được phân công.");
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        try
        {
            await using var stream = UploadFile.OpenReadStream();
            var result = await _docService.UploadAsync(
                stream, UploadFile.FileName, UploadFile.ContentType, UploadFile.Length,
                SubjectId, userId, Title, ChapterId);

            TempData["Success"] = result.Outcome switch
            {
                UploadOutcome.Created => $"✅ Đã upload & index '{result.Document.Title}' ({result.Document.ChunkCount} chunks).",
                UploadOutcome.Replaced => $"🔄 Đã thay thế & index lại '{result.Document.Title}' ({result.Document.ChunkCount} chunks).",
                UploadOutcome.Duplicate => "⚠️ File này đã được upload trước đó (nội dung giống hệt).",
                _ => "Upload thành công."
            };

            await _notifier.ActivityAsync("📄", "Tài liệu", Actor, $"Tải lên tài liệu \"{result.Document.Title}\" ({result.Document.Status})");
            return RedirectToPage("/Documents/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Lỗi khi upload: {ex.Message}");
            return Page();
        }
    }
}


