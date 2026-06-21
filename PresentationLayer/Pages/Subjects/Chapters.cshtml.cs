using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Subjects;

[Authorize(Policy = "LecturerOrAdmin")]
public class ChaptersModel : PageModel
{
    private readonly IChapterService _chapterService;
    private readonly ISubjectService _subjectService;

    public ChaptersModel(IChapterService chapterService, ISubjectService subjectService)
    {
        _chapterService = chapterService;
        _subjectService = subjectService;
    }

    public string SubjectId { get; private set; } = "";
    public string SubjectName { get; private set; } = "";
    public List<ServiceLayer.DTOs.ChapterDto> Chapters { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string subjectId)
    {
        var subject = await _subjectService.GetByIdAsync(subjectId);
        if (subject == null) return NotFound();

        SubjectId = subjectId;
        SubjectName = subject.Name;
        Chapters = await _chapterService.GetBySubjectAsync(subjectId);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        string subjectId, string title, string description, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Tên chương không được để trống.";
            return RedirectToPage(new { subjectId });
        }

        await _chapterService.CreateAsync(subjectId, title, description ?? "", orderIndex);
        TempData["Success"] = $"Đã thêm chương \"{title}\" thành công.";
        return RedirectToPage(new { subjectId });
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string id, string subjectId, string title, string description, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Tên chương không được để trống.";
            return RedirectToPage(new { subjectId });
        }

        await _chapterService.UpdateAsync(id, title, description ?? "", orderIndex);
        TempData["Success"] = $"Đã cập nhật chương \"{title}\".";
        return RedirectToPage(new { subjectId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string subjectId)
    {
        await _chapterService.DeleteAsync(id);
        TempData["Success"] = "Đã xóa chương. Tài liệu trong chương vẫn được giữ lại.";
        return RedirectToPage(new { subjectId });
    }
}


