using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Auth;

public class VerifyEmailModel : PageModel
{
    private readonly IUserService _users;
    public VerifyEmailModel(IUserService users) => _users = users;

    public string? Message { get; private set; }
    public bool IsSuccess { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Message = "Mã xác thực (token) không hợp lệ hoặc thiếu.";
            IsSuccess = false;
            return Page();
        }

        var (ok, err) = await _users.VerifyEmailAsync(token);
        IsSuccess = ok;
        Message = ok ? "Xác thực tài khoản thành công! Mời bạn đăng nhập." : (err ?? "Xác thực tài khoản thất bại.");
        return Page();
    }
}
