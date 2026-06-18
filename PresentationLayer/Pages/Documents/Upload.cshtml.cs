using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Pages.Documents;

[Authorize(Policy = "CanUploadDocuments")]
public class UploadModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;

    public UploadModel(IDocumentService docService, ISubjectService subjectService, IChapterService chapterService)
    {
        _docService = docService;
        _subjectService = subjectService;
        _chapterService = chapterService;
    }

    public List<Subject> Subjects { get; private set; } = [];
    public List<Chapter> Chapters { get; private set; } = [];

    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public string SubjectId { get; set; } = "";
    [BindProperty] public string? ChapterId { get; set; }
    [BindProperty] public string? Title { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Upload tài liệu";
        ViewData["TopbarTitle"] = "⬆️ Upload tài liệu";
        Subjects = await _subjectService.GetAllAsync();

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var assignedSubjectId = User.FindFirst("AssignedSubjectId")?.Value;

        if (role != "Admin")
        {
            if (!string.IsNullOrEmpty(assignedSubjectId))
            {
                Subjects = Subjects.Where(s => s.Id == assignedSubjectId).ToList();
                SubjectId = assignedSubjectId;
            }
            else
            {
                Subjects.Clear(); // Không có môn được phân công -> Không được upload
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "Upload tài liệu";
        Subjects = await _subjectService.GetAllAsync();

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var assignedSubjectId = User.FindFirst("AssignedSubjectId")?.Value;

        if (role != "Admin")
        {
            if (!string.IsNullOrEmpty(assignedSubjectId))
            {
                Subjects = Subjects.Where(s => s.Id == assignedSubjectId).ToList();
            }
            else
            {
                Subjects.Clear();
            }
        }

        if (UploadFile == null || UploadFile.Length == 0)
        {
            ModelState.AddModelError("File", "Vui lòng chọn file.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SubjectId))
        {
            ModelState.AddModelError("SubjectId", "Vui lòng chọn môn học.");
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        if (role != "Admin")
        {
            if (string.IsNullOrEmpty(assignedSubjectId))
            {
                ModelState.AddModelError("", "Bạn chưa được phân công môn học nào nên không thể upload.");
                return Page();
            }
            if (SubjectId != assignedSubjectId)
            {
                ModelState.AddModelError("SubjectId", "Bạn chỉ có quyền upload cho môn học được phân công.");
                return Page();
            }
        }

        try
        {
            await using var stream = UploadFile.OpenReadStream();
            var result = await _docService.UploadAsync(
                stream, UploadFile.FileName, UploadFile.ContentType, UploadFile.Length,
                SubjectId, userId, Title, ChapterId);

            TempData["Success"] = result.Outcome switch
            {
                UploadOutcome.Created => $"✅ Đã upload '{result.Document.Title}' thành công.",
                UploadOutcome.Replaced => $"🔄 Đã thay thế phiên bản cũ của '{result.Document.Title}'.",
                UploadOutcome.Duplicate => "⚠️ File này đã được upload trước đó (nội dung giống hệt).",
                _ => "Upload thành công."
            };

            return RedirectToPage("/Documents/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Lỗi khi upload: {ex.Message}");
            return Page();
        }
    }
}
