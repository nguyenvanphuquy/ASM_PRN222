using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IAllowedEmailService _allowedEmails;
    public AuthService(IUserRepository userRepo, IAllowedEmailService allowedEmails)
    {
        _userRepo = userRepo;
        _allowedEmails = allowedEmails;
    }

    public Task<bool> IsEmailAllowedAsync(string email) => _allowedEmails.IsAllowedAsync(email);

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new LoginResult(false, "Username và password không được trống", null, null, null, null, null);

        var user = await _userRepo.GetByUsernameAsync(username.Trim());
        if (user is null)
            return new LoginResult(false, "Tài khoản không tồn tại", null, null, null, null, null);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new LoginResult(false, "Sai mật khẩu", null, null, null, null, null);

        // Tài khoản do Admin tạo phải xác thực email mới được đăng nhập.
        if (!user.IsEmailVerified)
            return new LoginResult(false, "Tài khoản chưa được kích hoạt. Vui lòng kiểm tra email và bấm link kích hoạt.", null, null, null, null, null);

        return new LoginResult(true, null, user.Id, user.Username, user.FullName, user.Role, user.AvatarPath, user.CanUploadDocuments, user.AssignedSubjectId);
    }

    public async Task<bool> UsernameExistsAsync(string username)
        => await _userRepo.GetByUsernameAsync(username.Trim()) is not null;

    public async Task<RegisterResult> RegisterAsync(string username, string email, string password, string fullName)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new RegisterResult(false, "Username và password bắt buộc");
        if (password.Length < 6)
            return new RegisterResult(false, "Mật khẩu phải ít nhất 6 ký tự");

        // Whitelist: nếu admin đã bật danh sách email cho phép, email phải nằm trong đó.
        if (!await _allowedEmails.IsAllowedAsync(email ?? string.Empty))
            return new RegisterResult(false, "Email của bạn chưa được cho phép đăng ký. Vui lòng liên hệ quản trị viên.");

        var existing = await _userRepo.GetByUsernameAsync(username.Trim());
        if (existing is not null)
            return new RegisterResult(false, "Username đã tồn tại");

        var user = new User
        {
            Username = username.Trim(),
            Email = email?.Trim() ?? string.Empty,
            FullName = fullName?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Student"
        };
        await _userRepo.CreateAsync(user);
        return new RegisterResult(true, null);
    }

    public async Task EnsureSeedUsersAsync()
    {
        var admin = await _userRepo.GetByUsernameAsync("admin");
        if (admin is null)
        {
            await _userRepo.CreateAsync(new User
            {
                Username = "admin",
                Email = "admin@chatbot.local",
                FullName = "Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                IsEmailVerified = true
            });
        }

        var lecturer = await _userRepo.GetByUsernameAsync("lecturer");
        if (lecturer is null)
        {
            await _userRepo.CreateAsync(new User
            {
                Username = "lecturer",
                Email = "lecturer@chatbot.local",
                FullName = "Giảng viên Demo",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("lecturer123"),
                Role = "Lecturer",
                IsEmailVerified = true
            });
        }

        var student = await _userRepo.GetByUsernameAsync("student");
        if (student is null)
        {
            await _userRepo.CreateAsync(new User
            {
                Username = "student",
                Email = "student@chatbot.local",
                FullName = "Sinh viên Demo",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("student123"),
                Role = "Student",
                IsEmailVerified = true
            });
        }
    }
}
