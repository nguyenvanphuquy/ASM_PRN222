using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

/// <summary>
/// Hub thông báo real-time — mỗi user kết nối vào group riêng theo UserId.
/// Server có thể push notification tới bất kỳ user nào bằng INotificationService.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Thêm connection vào group theo UserId — dùng để gửi notification riêng
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }
}
