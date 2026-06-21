using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API so sánh tài liệu bằng AI (File Comparison).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class FileComparisonController : ControllerBase
{
    private readonly IFileComparisonService _comparisonService;

    public FileComparisonController(IFileComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    /// <summary>So sánh 2 tài liệu đã lưu trong hệ thống dựa trên ID.</summary>
    [HttpPost("compare-by-id")]
    [ProducesResponseType(typeof(FileComparisonResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileComparisonResult>> CompareById([FromBody] CompareByIdRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _comparisonService.CompareAsync(req.DocumentId1, req.DocumentId2, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"So sánh tài liệu thất bại: {ex.Message}" });
        }
    }

    /// <summary>Upload trực tiếp 2 file để so sánh nội dung bằng AI (không lưu vào CSDL).</summary>
    [HttpPost("compare-files")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileComparisonResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileComparisonResult>> CompareFiles(
        [FromForm] CompareFilesRequest req,
        CancellationToken ct)
    {
        if (req.File1 is null || req.File1.Length == 0)
            return BadRequest(new { message = "File 1 không hợp lệ hoặc trống." });
        if (req.File2 is null || req.File2.Length == 0)
            return BadRequest(new { message = "File 2 không hợp lệ hoặc trống." });

        try
        {
            using var stream1 = req.File1.OpenReadStream();
            using var stream2 = req.File2.OpenReadStream();

            var result = await _comparisonService.CompareStreamsAsync(
                stream1, req.File1.FileName, req.File1.ContentType,
                stream2, req.File2.FileName, req.File2.ContentType,
                ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"So sánh file thất bại: {ex.Message}" });
        }
    }
}

public class CompareFilesRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng chọn File 1")]
    public IFormFile File1 { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng chọn File 2")]
    public IFormFile File2 { get; set; } = null!;
}
