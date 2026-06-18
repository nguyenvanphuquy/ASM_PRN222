using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password);
    Task<bool> UsernameExistsAsync(string username);
    // True nếu email được phép đăng ký (whitelist trống = mở cho mọi email).
    Task<bool> IsEmailAllowedAsync(string email);
    Task<RegisterResult> RegisterAsync(string username, string email, string password, string fullName);
    Task EnsureSeedUsersAsync();
}
