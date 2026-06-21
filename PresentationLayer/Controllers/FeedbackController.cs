using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API quản lý Phản hồi (Feedback) &amp; Trả lời (Replies) — đầy đủ CRUD.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _service;

    public FeedbackController(IFeedbackService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả phản hồi cùng các câu trả lời liên quan.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeedbackResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeedbackResponse>>> GetAll()
    {
        var feedbackList = await _service.GetAllAsync();
        var feedbackIds = feedbackList.Select(f => f.Id).ToList();
        var replies = await _service.GetRepliesForAsync(feedbackIds);

        var response = feedbackList.Select(f => ToResponse(f, replies.Where(r => r.FeedbackId == f.Id)));
        return Ok(response);
    }

    /// <summary>Lấy phản hồi của một người dùng cụ thể.</summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<FeedbackResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeedbackResponse>>> GetByUser(string userId)
    {
        var feedbackList = await _service.GetByUserAsync(userId);
        var feedbackIds = feedbackList.Select(f => f.Id).ToList();
        var replies = await _service.GetRepliesForAsync(feedbackIds);

        var response = feedbackList.Select(f => ToResponse(f, replies.Where(r => r.FeedbackId == f.Id)));
        return Ok(response);
    }

    /// <summary>Lấy thống kê đánh giá (Tổng số lượng phản hồi và Điểm trung bình).</summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var (total, average) = await _service.GetStatsAsync();
        return Ok(new { total, average });
    }

    /// <summary>Gửi phản hồi mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeedbackResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackResponse>> Create([FromBody] FeedbackRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var fullName = User.FindFirst("FullName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
        var userAvatar = User.FindFirst("AvatarPath")?.Value;

        var created = await _service.CreateAsync(userId, fullName, userAvatar, req.Rating, req.Content);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, ToResponse(created, Enumerable.Empty<FeedbackReply>()));
    }

    /// <summary>Trả lời phản hồi.</summary>
    [HttpPost("{id}/replies")]
    [ProducesResponseType(typeof(FeedbackReplyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReply(string id, [FromBody] FeedbackReplyRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var fullName = User.FindFirst("FullName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
        var userAvatar = User.FindFirst("AvatarPath")?.Value;
        var isAdmin = User.IsInRole("Admin");

        await _service.AddReplyAsync(id, userId, fullName, userAvatar, req.Content, isAdmin);
        return Ok(new { message = "Gửi câu trả lời thành công." });
    }

    /// <summary>Xoá phản hồi (Chỉ Admin).</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        // Kiểm tra xem feedback có tồn tại không
        var list = await _service.GetAllAsync();
        var exists = list.Any(f => f.Id == id);
        if (!exists) return NotFound(new { message = "Không tìm thấy phản hồi." });

        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Xoá câu trả lời phản hồi.</summary>
    [HttpDelete("replies/{replyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteReply(string replyId)
    {
        var reply = await _service.GetReplyAsync(replyId);
        if (reply is null) return NotFound(new { message = "Không tìm thấy câu trả lời." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var isAdmin = User.IsInRole("Admin");

        // Chỉ Admin hoặc người tạo ra reply đó mới được xoá
        if (!isAdmin && reply.UserId != userId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền xoá câu trả lời này." });

        await _service.DeleteReplyAsync(replyId);
        return NoContent();
    }

    private static FeedbackResponse ToResponse(Feedback f, IEnumerable<FeedbackReply> replies) => new()
    {
        Id = f.Id,
        UserId = f.UserId,
        UserName = f.UserName,
        UserAvatar = f.UserAvatar,
        Rating = f.Rating,
        Content = f.Content,
        CreatedAt = f.CreatedAt,
        Replies = replies.Select(ToReplyResponse).ToList()
    };

    private static FeedbackReplyResponse ToReplyResponse(FeedbackReply r) => new()
    {
        Id = r.Id,
        FeedbackId = r.FeedbackId,
        UserId = r.UserId,
        UserName = r.UserName,
        UserAvatar = r.UserAvatar,
        Content = r.Content,
        IsAdmin = r.IsAdmin,
        CreatedAt = r.CreatedAt
    };
}
