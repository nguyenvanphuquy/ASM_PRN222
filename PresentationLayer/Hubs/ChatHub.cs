using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Client gọi hàm này để gửi câu hỏi. Hub sẽ gọi AI rồi push kết quả về.
    /// </summary>
    public async Task SendMessageAsync(string sessionId, string question)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        if (string.IsNullOrWhiteSpace(question)) return;

        // Xác thực session thuộc về user này
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null || session.UserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "Session không hợp lệ.");
            return;
        }

        // Báo cho client biết AI đang xử lý
        await Clients.Caller.SendAsync("Thinking");

        try
        {
            var result = await _chatService.AskAsync(sessionId, userId, question);

            // Gửi câu trả lời + nguồn tham khảo về client
            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                answer = result.Answer,
                sources = result.Sources.Select(s => new
                {
                    documentName = s.DocumentName,
                    page = s.Page,
                    confidenceScore = s.ConfidenceScore
                })
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", "Lỗi khi gọi AI: " + ex.Message);
        }
    }
}
