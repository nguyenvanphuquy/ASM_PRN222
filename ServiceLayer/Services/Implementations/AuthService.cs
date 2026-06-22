using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    public AuthService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

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


