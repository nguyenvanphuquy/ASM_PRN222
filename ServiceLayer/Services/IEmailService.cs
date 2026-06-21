namespace ServiceLayer.Services;

public interface IEmailService
{
    Task SendOtpAsync(string toEmail, string toName, string otpCode);

    // Gửi thông tin đăng nhập (username + password) kèm link kích hoạt tài khoản
    // cho tài khoản do Admin tạo.
    Task SendAccountCreatedAsync(string toEmail, string toName, string username, string password, string verifyUrl);
}


