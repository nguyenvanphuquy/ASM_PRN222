using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ServiceLayer.Settings;

namespace ServiceLayer.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    public EmailService(IOptions<EmailSettings> settings) => _settings = settings.Value;

    public async Task SendOtpAsync(string toEmail, string toName, string otpCode)
    {
        if (!_settings.IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình email gửi OTP (Email:FromEmail / Email:AppPassword trong appsettings.json).");

        var subject = "Mã xác thực đăng ký ChatBot PRN222";
        var body = $@"
<div style='font-family:Arial,sans-serif;max-width:480px;margin:auto'>
  <h2 style='color:#4f46e5'>ChatBot PRN222</h2>
  <p>Xin chào <strong>{WebUtility.HtmlEncode(toName)}</strong>,</p>
  <p>Mã xác thực (OTP) để hoàn tất đăng ký tài khoản của bạn là:</p>
  <div style='font-size:32px;font-weight:bold;letter-spacing:8px;color:#1f2937;
              background:#f3f6fb;padding:16px;text-align:center;border-radius:8px'>{otpCode}</div>
  <p style='color:#6b7280;font-size:13px;margin-top:16px'>Mã có hiệu lực trong 5 phút. Nếu bạn không yêu cầu đăng ký, hãy bỏ qua email này.</p>
</div>";

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        await SendAsync(message);
    }

    public async Task SendAccountCreatedAsync(string toEmail, string toName, string username, string password, string verifyUrl)
    {
        if (!_settings.IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình email gửi tài khoản (Email:FromEmail / Email:AppPassword trong appsettings.json).");

        var subject = "Tài khoản ChatBot PRN222 của bạn";
        var body = $@"
<div style='font-family:Arial,sans-serif;max-width:480px;margin:auto'>
  <h2 style='color:#4f46e5'>ChatBot PRN222</h2>
  <p>Xin chào <strong>{WebUtility.HtmlEncode(toName)}</strong>,</p>
  <p>Quản trị viên đã tạo một tài khoản cho bạn. Thông tin đăng nhập:</p>
  <div style='background:#f3f6fb;padding:16px;border-radius:8px;font-size:15px'>
    <div>Tên đăng nhập: <strong>{WebUtility.HtmlEncode(username)}</strong></div>
    <div>Mật khẩu: <strong>{WebUtility.HtmlEncode(password)}</strong></div>
  </div>
  <p style='margin-top:16px'>Vui lòng bấm nút bên dưới để <strong>kích hoạt tài khoản</strong>. Bạn chỉ đăng nhập được sau khi đã kích hoạt:</p>
  <p style='text-align:center;margin:24px 0'>
    <a href='{verifyUrl}' style='background:#4f46e5;color:#fff;text-decoration:none;
        padding:12px 28px;border-radius:8px;font-weight:bold;display:inline-block'>Kích hoạt tài khoản</a>
  </p>
  <p style='color:#6b7280;font-size:13px'>Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:<br/>
    <a href='{verifyUrl}'>{verifyUrl}</a></p>
  <p style='color:#6b7280;font-size:13px;margin-top:16px'>Sau khi đăng nhập, bạn nên đổi mật khẩu tại trang Hồ sơ cá nhân.</p>
</div>";

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        await SendAsync(message);
    }

    private async Task SendAsync(MailMessage message)
    {
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true, // STARTTLS on port 587
            Credentials = new NetworkCredential(_settings.FromEmail, _settings.AppPassword)
        };
        await client.SendMailAsync(message);
    }
}
