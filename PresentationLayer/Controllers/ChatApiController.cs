using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>Các API endpoint cho hệ thống Chat AI.</summary>
[ApiController]
[Route("api/chat")]
[Authorize]
[Produces("application/json")]
public class ChatApiController : ControllerBase
{
    private readonly IChatService _chatService;
    public ChatApiController(IChatService chatService) => _chatService = chatService;

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    /// <summary>Lấy danh sách phiên chat của người dùng hiện tại.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _chatService.GetSessionsAsync(UserId);
        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.Title,
            s.SubjectId,
            s.CreatedAt,
            s.UpdatedAt
        }));
    }

    /// <summary>Tạo phiên chat mới.</summary>
    /// <param name="req">SubjectId (có thể null để hỏi không gắn môn)</param>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(object), 201)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        var session = await _chatService.CreateSessionAsync(UserId, req.SubjectId);
        return CreatedAtAction(nameof(GetMessages), new { sessionId = session.Id }, new { id = session.Id });
    }

    /// <summary>Lấy lịch sử tin nhắn của một phiên chat.</summary>
    [HttpGet("sessions/{sessionId}/messages")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMessages(string sessionId)
    {
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null || session.UserId != UserId) return NotFound();

        var messages = await _chatService.GetMessagesAsync(sessionId);
        return Ok(messages.Select(m => new
        {
            m.Id,
            m.Role,
            m.Content,
            m.CreatedAt,
            Sources = m.Sources.Select(s => new
            {
                s.DocumentName,
                s.Page,
                s.ConfidenceScore
            })
        }));
    }

    /// <summary>Gửi câu hỏi tới AI và nhận câu trả lời (REST, không real-time).</summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Ask([FromBody] AskRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(new { error = "Question không được để trống." });

        var session = await _chatService.GetSessionAsync(req.SessionId);
        if (session == null || session.UserId != UserId) return NotFound();

        try
        {
            var result = await _chatService.AskAsync(req.SessionId, UserId, req.Question);
            return Ok(new
            {
                answer = result.Answer,
                sources = result.Sources.Select(s => new
                {
                    documentName = s.DocumentName,
                    page = s.Page,
                    confidenceScore = s.ConfidenceScore,
                    snippet = s.Snippet
                })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Xóa một phiên chat.</summary>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null || session.UserId != UserId) return NotFound();

        await _chatService.DeleteSessionAsync(sessionId);
        return NoContent();
    }
}

public record CreateSessionRequest(string? SubjectId);
public record AskRequest(string SessionId, string Question);
