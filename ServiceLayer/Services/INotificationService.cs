namespace ServiceLayer.Services;

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
}
