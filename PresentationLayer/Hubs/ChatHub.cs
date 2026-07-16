using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IBillingService _billing;
    private readonly INotificationService _notifier;

    public ChatHub(IChatService chatService, IBillingService billing, INotificationService notifier)
    {
        _chatService = chatService;
        _billing = billing;
        _notifier = notifier;
    }

    /// <summary>
    /// Client gọi hàm này để gửi câu hỏi. Hub sẽ gọi AI rồi push kết quả về.
    /// </summary>
    public async Task SendMessageAsync(string sessionId, string question)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        if (string.IsNullOrWhiteSpace(question)) return;

        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null || session.UserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "Session không hợp lệ.");
            return;
        }

        await Clients.Caller.SendAsync("Thinking");

        try
        {
            var result = await _chatService.AskAsync(sessionId, userId, question);

            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                answer = result.Answer,
                sources = result.Sources.Select(s => new
                {
                    documentId = s.DocumentId,
                    documentName = s.DocumentName,
                    page = s.Page,
                    chunkIndex = s.ChunkIndex,
                    snippet = s.Snippet,
                    confidenceScore = s.ConfidenceScore
                })
            });

            // Cập nhật số dư token realtime (sinh viên bị trừ sau mỗi lần hỏi).
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (role == "Student")
            {
                var bal = await _billing.GetBalanceAsync(userId);
                await Clients.Caller.SendAsync("TokenBalanceChanged", new
                {
                    remaining = bal.Remaining,
                    granted = bal.Granted,
                    used = bal.Used
                });
                await _notifier.TokenBalanceChangedAsync(userId, bal.Remaining, bal.Granted, bal.Used);
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", "Lỗi khi gọi AI: " + ex.Message);
        }
    }
}
