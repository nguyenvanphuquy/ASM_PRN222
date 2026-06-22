using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ISubjectService _subjectService;
    private readonly IEmailService _emailService;

    public IndexModel(IUserService userService, ISubjectService subjectService, IEmailService emailService)
    {
        _userService = userService;
        _subjectService = subjectService;
        _emailService = emailService;
    }

    public List<ServiceLayer.DTOs.UserDto> Users { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];

    // Thống kê nhanh theo vai trò (hiển thị stat cards ở đầu trang)
    public int TotalCount { get; private set; }
    public int AdminCount { get; private set; }
    public int LecturerCount { get; private set; }
    public int StudentCount { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Người dùng";
        ViewData["TopbarTitle"] = "👥 Quản lý người dùng";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Users = await _userService.GetAllAsync();
        Subjects = await _subjectService.GetAllAsync();

        TotalCount = Users.Count;
        AdminCount = Users.Count(u => u.Role == "Admin");
        LecturerCount = Users.Count(u => u.Role == "Lecturer");
        StudentCount = Users.Count(u => u.Role == "Student");
    }

    public async Task<IActionResult> OnPostCreateAsync(string username, string email, string fullName, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            TempData["Error"] = "Vui lòng điền đầy đủ thông tin tạo tài khoản.";
            return RedirectToPage();
        }

        var (ok, err, token) = await _userService.CreateAsync(
            username.Trim(), email.Trim(), fullName.Trim(), password, string.IsNullOrWhiteSpace(role) ? "Student" : role);

        if (!ok)
        {
            TempData["Error"] = err;
            return RedirectToPage();
        }

        // Tài khoản tạo ra đang ở trạng thái CHƯA kích hoạt → phải gửi email kèm
        // thông tin đăng nhập + link kích hoạt thì người dùng mới đăng nhập được.
        var verifyUrl = Url.Page("/Auth/VerifyEmail", pageHandler: null,
            values: new { token }, protocol: Request.Scheme) ?? $"/Auth/VerifyEmail?token={token}";

        try
        {
            await _emailService.SendAccountCreatedAsync(email.Trim(), fullName.Trim(), username.Trim(), password, verifyUrl);
            TempData["Success"] = $"Đã tạo tài khoản {username} và gửi email kích hoạt + thông tin đăng nhập tới {email.Trim()}.";
        }
        catch (Exception ex)
        {
            // Không gửi được email (chưa cấu hình SMTP / lỗi mạng) → hiển thị link để Admin gửi thủ công.
            TempData["Error"] = $"Đã tạo tài khoản {username} nhưng KHÔNG gửi được email ({ex.Message}). " +
                                $"Hãy gửi thủ công link kích hoạt cho người dùng: {verifyUrl}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(string id, string role)
    {
        var (ok, err) = await _userService.UpdateRoleAsync(id, role);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật vai trò." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "Mật khẩu mới tối thiểu 6 ký tự.";
            return RedirectToPage();
        }

        var (ok, err) = await _userService.ResetPasswordAsync(id, newPassword);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã đặt lại mật khẩu." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (ok, err) = await _userService.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xoá người dùng." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignSubjectAsync(string id, string subjectId)
    {
        var (ok, err) = await _userService.SetUploadPermissionAsync(id, true, subjectId);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã phân công môn học thành công." : err;
        return RedirectToPage();
    }
}


