using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Services;

namespace PresentationLayer.Controllers;

/// <summary>API endpoint trả về danh sách Chapter theo Subject (dùng cho Upload form).</summary>
[ApiController]
[Route("api/chapters")]
[Authorize]
[Produces("application/json")]
public class ChapterApiController : ControllerBase
{
    private readonly IChapterService _chapterService;
    public ChapterApiController(IChapterService chapterService) => _chapterService = chapterService;

    /// <summary>Lấy danh sách chương theo môn học.</summary>
    [HttpGet("{subjectId}")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetBySubject(string subjectId)
    {
        var chapters = await _chapterService.GetBySubjectAsync(subjectId);
        return Ok(chapters.OrderBy(c => c.OrderIndex).Select(c => new
        {
            c.Id,
            c.Title,
            c.Description,
            c.OrderIndex,
            c.SubjectId
        }));
    }

    /// <summary>Lấy toàn bộ chương (Admin only).</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var chapters = await _chapterService.GetAllAsync();
        return Ok(chapters.Select(c => new
        {
            c.Id,
            c.Title,
            c.Description,
            c.OrderIndex,
            c.SubjectId
        }));
    }
}
