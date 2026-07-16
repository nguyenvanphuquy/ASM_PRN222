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
    /// Phát sự kiện realtime khi một tài liệu được THÊM MỚI hoặc XOÁ,
    /// để trang Tài liệu tự thêm/xoá dòng mà không cần reload.
    /// action: "created" (kèm <paramref name="document"/>) | "deleted" (kèm <paramref name="documentId"/>).
    /// </summary>
    Task DocumentChangedAsync(string action, ServiceLayer.DTOs.DocumentDto? document, string? documentId = null);

    /// <summary>
    /// Phát sự kiện realtime báo một tài khoản vừa đổi trạng thái.
    /// action: "verified" (vừa kích hoạt email) | "role" (đổi vai trò, value = vai trò mới) | "deleted".
    /// Trang Quản lý người dùng lắng nghe để cập nhật badge/danh sách mà không cần reload.
    /// </summary>
    Task UserChangedAsync(string action, string userId, string? value = null);

    /// <summary>
    /// Phát sự kiện realtime khi có giao dịch mua gói mới — trang Báo cáo &amp; Thống kê
    /// của admin cập nhật doanh thu/token mà không cần reload.
    /// </summary>
    Task PackagePurchasedAsync(string packageName, long amountVnd, int tokens, string? userName = null);

    /// <summary>
    /// Phát sự kiện realtime khi danh mục GÓI thay đổi (admin tạo/sửa/xoá gói) —
    /// trang Cửa hàng gói của sinh viên tự nạp lại danh sách gói mà không cần reload.
    /// action: created | updated | deleted.
    /// </summary>
    Task PackagesChangedAsync(string action);

    /// <summary>
    /// Ghi nhận một HÀNH ĐỘNG trên hệ thống và phát realtime tới trang "Nhật ký hệ thống"
    /// (giám sát) để admin thấy ngay mọi hoạt động: đăng nhập, tải tài liệu, mua/hủy gói,
    /// tạo/sửa/xoá tài khoản-gói, đổi cấu hình...
    /// </summary>
    Task ActivityAsync(string icon, string category, string actor, string description);

    /// <summary>
    /// Phát sự kiện realtime khi phản hồi thay đổi (tạo / trả lời / xoá).
    /// Trang Phản hồi và Dashboard lắng nghe để cập nhật mà không cần reload thủ công.
    /// action: created | reply | deleted.
    /// </summary>
    Task FeedbackChangedAsync(string action, string feedbackId, string? userId = null, string? preview = null);

    /// <summary>
    /// Thông báo toast tới mọi client thuộc một vai trò (group role-{role}).
    /// Không ghi DB — dùng cho cảnh báo realtime (vd. admin có phản hồi mới).
    /// </summary>
    Task NotifyRoleAsync(string role, string type, string title, string message);

    /// <summary>
    /// Phát sự kiện số dư token của một user vừa thay đổi (sau chat / mua / hủy gói).
    /// Layout và trang Cửa hàng cập nhật badge token realtime.
    /// </summary>
    Task TokenBalanceChangedAsync(string userId, int remaining, int granted, int used);
}


