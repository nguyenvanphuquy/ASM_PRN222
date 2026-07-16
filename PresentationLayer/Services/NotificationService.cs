using Microsoft.AspNetCore.SignalR;
using PresentationLayer.Hubs;
using ServiceLayer.Services.Interfaces;

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

    public async Task DocumentStatusChangedAsync(string documentId, string status)
    {
        // Phát tới mọi client — trang Tài liệu sẽ tự lọc theo documentId có trên bảng của mình.
        await _hub.Clients.All
            .SendAsync("DocumentStatusChanged", new { documentId, status });
    }

    public async Task DocumentChangedAsync(string action, ServiceLayer.DTOs.DocumentDto? document, string? documentId = null)
    {
        // Phát tới mọi client — trang Tài liệu tự thêm/xoá dòng tương ứng (idempotent + lọc theo filter).
        object payload = action == "deleted"
            ? new { action, documentId = documentId ?? document?.Id }
            : new
            {
                action,
                document = document == null ? null : (object)new
                {
                    id = document.Id,
                    title = document.Title,
                    fileName = document.FileName,
                    subjectId = document.SubjectId,
                    status = document.Status,
                    fileSize = document.FileSize,
                    uploadedAt = document.UploadedAt
                }
            };

        await _hub.Clients.All.SendAsync("DocumentChanged", payload);
    }

    public async Task UserChangedAsync(string action, string userId, string? value = null)
    {
        // Phát tới mọi client — trang Quản lý người dùng tự lọc theo userId có trên bảng.
        await _hub.Clients.All
            .SendAsync("UserChanged", new { action, userId, value });
    }

    public async Task PackagePurchasedAsync(string packageName, long amountVnd, int tokens, string? userName = null)
    {
        await _hub.Clients.All
            .SendAsync("PackagePurchased", new { packageName, amountVnd, tokens, userName, time = DateTime.Now.ToString("HH:mm") });
    }

    public async Task PackagesChangedAsync(string action)
    {
        await _hub.Clients.All
            .SendAsync("PackagesChanged", new { action, time = DateTime.Now.ToString("HH:mm") });
    }

    public async Task ActivityAsync(string icon, string category, string actor, string description)
    {
        // Persist trước — xoá tài liệu / đăng nhập… vẫn còn trong Nhật ký sau reload.
        var row = new DataAccessLayer.Entities.SystemActivity
        {
            Icon = string.IsNullOrWhiteSpace(icon) ? "•" : icon.Trim(),
            Category = category?.Trim() ?? "",
            Actor = actor?.Trim() ?? "",
            Description = description?.Trim() ?? "",
            CreatedAt = DateTime.UtcNow
        };
        if (row.Description.Length > 1000)
            row.Description = row.Description[..1000];
        if (row.Actor.Length > 200)
            row.Actor = row.Actor[..200];

        _db.SystemActivities.Add(row);
        await _db.SaveChangesAsync();

        await _hub.Clients.All
            .SendAsync("ActivityLogged", new
            {
                icon = row.Icon,
                category = row.Category,
                actor = row.Actor,
                description = row.Description,
                time = row.CreatedAt.ToLocalTime().ToString("HH:mm:ss")
            });
    }

    public async Task FeedbackChangedAsync(string action, string feedbackId, string? userId = null, string? preview = null)
    {
        await _hub.Clients.All.SendAsync("FeedbackChanged", new
        {
            action,
            feedbackId,
            userId,
            preview,
            time = DateTime.Now.ToString("HH:mm")
        });
    }

    public async Task NotifyRoleAsync(string role, string type, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(role)) return;
        await _hub.Clients.Group($"role-{role}")
            .SendAsync("ReceiveNotification", new
            {
                type,
                title,
                message,
                time = DateTime.Now.ToString("HH:mm"),
                isRead = false
            });
    }

    public async Task TokenBalanceChangedAsync(string userId, int remaining, int granted, int used)
    {
        await _hub.Clients.Group($"user-{userId}")
            .SendAsync("TokenBalanceChanged", new { remaining, granted, used });
    }
}
