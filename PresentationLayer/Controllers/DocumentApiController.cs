using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Services;
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
    public DocumentApiController(IDocumentService documentService) => _documentService = documentService;

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
            d.QualityScore,
            d.FileSize,
            d.UploadedAt,
            d.UploadedBy
        }));
    }

    /// <summary>Lấy chi tiết một tài liệu kèm kết quả AI Quality Check.</summary>
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
            doc.QualityScore,
            doc.QualitySummary,
            doc.QualityWarnings,
            doc.FileSize,
            doc.UploadedAt,
            doc.UploadedBy
        });
    }

    /// <summary>Phê duyệt tài liệu đang ở trạng thái Reviewing → bắt đầu Chunk & Index.</summary>
    [HttpPost("{id}/approve")]
    [Authorize(Policy = "LecturerOrAdmin")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        var success = await _documentService.ApproveAsync(id);
        if (!success) return BadRequest(new { error = "Chỉ có thể duyệt tài liệu ở trạng thái Reviewing." });

        return Ok(new { message = "Tài liệu đã được duyệt và đang được index." });
    }

    /// <summary>Từ chối tài liệu đang ở trạng thái Reviewing.</summary>
    [HttpPost("{id}/reject")]
    [Authorize(Policy = "LecturerOrAdmin")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Reject(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        var success = await _documentService.RejectAsync(id);
        if (!success) return BadRequest(new { error = "Chỉ có thể từ chối tài liệu ở trạng thái Reviewing." });

        return Ok(new { message = "Tài liệu đã bị từ chối." });
    }

    /// <summary>Xóa tài liệu (Admin only).</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        await _documentService.DeleteAsync(id);
        return NoContent();
    }
}

