using DataAccessLayer.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Helpers;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>Các API endpoint cho quản lý tài liệu.</summary>
[ApiController]
[Route("api/documents")]
[Authorize]
[Produces("application/json")]
public class DocumentApiController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly INotificationService _notifier;

    public DocumentApiController(
        IDocumentService documentService,
        IDocumentChunkRepository chunkRepo,
        INotificationService notifier)
    {
        _documentService = documentService;
        _chunkRepo = chunkRepo;
        _notifier = notifier;
    }

    /// <summary>Lấy danh sách tài liệu. Lọc theo subjectId nếu cần.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetDocuments([FromQuery] string? subjectId = null)
    {
        var docs = string.IsNullOrEmpty(subjectId)
            ? await _documentService.GetAllAsync()
            : await _documentService.GetBySubjectAsync(subjectId);

        return Ok(docs.Select(d => new
        {
            d.Id,
            d.Title,
            d.FileName,
            d.SubjectId,
            d.ChapterId,
            d.Status,
            d.ChunkCount,
            d.FileSize,
            d.UploadedAt,
            d.UploadedBy
        }));
    }

    /// <summary>Lấy chi tiết một tài liệu.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDocument(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        return Ok(new
        {
            doc.Id,
            doc.Title,
            doc.FileName,
            doc.SubjectId,
            doc.ChapterId,
            doc.Status,
            doc.ChunkCount,
            doc.FileSize,
            doc.UploadedAt,
            doc.UploadedBy
        });
    }

    /// <summary>Lấy nội dung đầy đủ của một chunk (dùng cho modal trích dẫn trong chat).</summary>
    [HttpGet("{documentId}/chunks/{chunkIndex:int}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetChunk(string documentId, int chunkIndex)
    {
        var chunk = await _chunkRepo.GetByDocumentAndIndexAsync(documentId, chunkIndex);
        if (chunk == null) return NotFound();

        return Ok(new
        {
            chunk.DocumentId,
            chunk.DocumentName,
            chunk.ChunkIndex,
            chunk.Page,
            content = chunk.Content
        });
    }

    /// <summary>Phê duyệt tài liệu đang ở trạng thái Reviewing → bắt đầu Chunk & Index. (Giảng viên — admin chỉ giám sát.)</summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        var success = await _documentService.ApproveAsync(id);
        if (!success) return BadRequest(new { error = "Chỉ có thể duyệt tài liệu ở trạng thái Reviewing." });

        return Ok(new { message = "Tài liệu đã được duyệt và đang được index." });
    }

    /// <summary>Từ chối tài liệu đang ở trạng thái Reviewing. (Giảng viên — admin chỉ giám sát.)</summary>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Reject(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        var success = await _documentService.RejectAsync(id);
        if (!success) return BadRequest(new { error = "Chỉ có thể từ chối tài liệu ở trạng thái Reviewing." });

        return Ok(new { message = "Tài liệu đã bị từ chối." });
    }

    /// <summary>Xóa tài liệu. (Giảng viên — admin chỉ giám sát, không được quản lý tài liệu.)</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        if (!SubjectDocumentAuth.CanManageSubject(User, doc.SubjectId))
            return Forbid();

        await _documentService.DeleteAsync(id);
        var actor = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Người dùng";
        await _notifier.ActivityAsync("🗑", "Tài liệu", actor, $"Xoá tài liệu \"{doc.Title}\"");
        return NoContent();
    }
}

