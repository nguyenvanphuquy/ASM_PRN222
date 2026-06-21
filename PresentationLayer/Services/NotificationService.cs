using Microsoft.AspNetCore.SignalR;
using PresentationLayer.Hubs;
using ServiceLayer.Services;

namespace PresentationLayer.Services;


/// <summary>
/// Implementation của INotificationService — dùng SignalR NotificationHub để push thông báo.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly DataAccessLayer.Context.AppDbContext _db;

    public NotificationService(IHubContext<NotificationHub> hub, DataAccessLayer.Context.AppDbContext db)
    {
        _hub = hub;
        _db = db;
    }

    public async Task SendAsync(string userId, string type, string title, string message)
    {
        var notif = new DataAccessLayer.Entities.Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group($"user-{userId}")
            .SendAsync("ReceiveNotification", new { id = notif.Id, type, title, message, time = notif.CreatedAt.ToLocalTime().ToString("HH:mm"), isRead = false });
    }

    public async Task BroadcastAsync(string type, string title, string message)
    {
        await _hub.Clients.All
            .SendAsync("ReceiveNotification", new { type, title, message, time = DateTime.Now.ToString("HH:mm") });
    }
}
