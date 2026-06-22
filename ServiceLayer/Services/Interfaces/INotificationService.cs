namespace ServiceLayer.Services.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Gửi thông báo tới một user cụ thể (theo UserId).
    /// </summary>
    /// <param name="userId">UserId của người nhận</param>
    /// <param name="type">success | error | info | warning</param>
    /// <param name="title">Tiêu đề thông báo</param>
    /// <param name="message">Nội dung thông báo</param>
    Task SendAsync(string userId, string type, string title, string message);

    /// <summary>Gửi thông báo tới tất cả user đang online.</summary>
    Task BroadcastAsync(string type, string title, string message);

    /// <summary>
    /// Phát sự kiện realtime báo trạng thái xử lý của một tài liệu đã thay đổi
    /// (Processing → Reviewing → Indexing → Indexed/Empty/Failed/Rejected).
    /// Trang Tài liệu lắng nghe để cập nhật badge trạng thái mà không cần reload.
    /// </summary>
    Task DocumentStatusChangedAsync(string documentId, string status);

    /// <summary>
    /// Phát sự kiện realtime báo một tài khoản vừa đổi trạng thái.
    /// action: "verified" (vừa kích hoạt email) | "role" (đổi vai trò, value = vai trò mới) | "deleted".
    /// Trang Quản lý người dùng lắng nghe để cập nhật badge/danh sách mà không cần reload.
    /// </summary>
    Task UserChangedAsync(string action, string userId, string? value = null);
}


