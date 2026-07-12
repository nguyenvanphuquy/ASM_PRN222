using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Documents;

[Authorize]
public class ViewModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;

    public ViewModel(
        IDocumentService documentService,
        ISubjectService subjectService,
        IChapterService chapterService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _chapterService = chapterService;
    }

    public ServiceLayer.DTOs.DocumentDto Document { get; set; } = default!;
    public List<ServiceLayer.DTOs.DocumentChunkDto> Chunks { get; set; } = new();
    public string? SubjectName { get; set; }
    public string? ChapterTitle { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null)
            return NotFound();

        Document = doc;
        Chunks = await _documentService.GetChunksAsync(id);

        var subject = (await _subjectService.GetAllAsync()).FirstOrDefault(s => s.Id == doc.SubjectId);
        SubjectName = subject != null ? $"{subject.Code} – {subject.Name}" : null;

        if (!string.IsNullOrEmpty(doc.ChapterId))
        {
            var chapters = await _chapterService.GetBySubjectAsync(doc.SubjectId);
            ChapterTitle = chapters.FirstOrDefault(c => c.Id == doc.ChapterId)?.Title;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        await _documentService.ApproveAsync(id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        await _documentService.RejectAsync(id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReChunkAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        try
        {
            var count = await _documentService.ReChunkAsync(id);
            if (count == null)
                TempData["Error"] = "Không tìm thấy tài liệu hoặc file gốc để Re-Chunk.";
            else
                TempData["Success"] = $"Đã Re-Chunk thành công: {count} đoạn.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Re-Chunk thất bại: {ex.Message}";
        }

        return RedirectToPage(new { id });
    }
}
