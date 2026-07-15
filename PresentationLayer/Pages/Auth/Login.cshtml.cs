using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IAuthService _auth;
    private readonly INotificationService _notifier;

    public LoginModel(IAuthService auth, INotificationService notifier)
    {
        _auth = auth;
        _notifier = notifier;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage(DashboardHome.PageFor(User));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Vui lòng điền đầy đủ thông tin.";
            return Page();
        }

        var result = await _auth.LoginAsync(Username, Password);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        var assigned = result.AssignedSubjectIds ?? Array.Empty<string>();
        var canUpload = result.Role == "Lecturer" && assigned.Count > 0;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId!),
            new(ClaimTypes.Name,           result.Username!),
            new("FullName",                result.FullName ?? result.Username!),
            new(ClaimTypes.Role,           result.Role ?? "Student"),
            new("CanUpload",               canUpload ? "true" : "false")
        };

        if (!string.IsNullOrEmpty(result.AvatarPath))
            claims.Add(new("AvatarPath", result.AvatarPath));
        if (assigned.Count > 0)
        {
            claims.Add(new("AssignedSubjects", string.Join(",", assigned)));
            claims.Add(new("AssignedSubjectId", assigned[0])); // legacy UI
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        await _notifier.ActivityAsync("🔑", "Đăng nhập", result.FullName ?? result.Username!,
            $"{result.Role ?? "Student"} đăng nhập hệ thống");

        // Chỉ giữ ReturnUrl nếu path đó phù hợp với role — tránh Lecturer bị
        // ném sang /Users (AdminOnly) → AccessDenied rồi mới về Dashboard.
        var home = DashboardHome.PathFor(result.Role);
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && IsAllowedReturnUrl(returnUrl, result.Role))
            return LocalRedirect(returnUrl);

        return LocalRedirect(home);
    }

    /// <summary>
    /// ReturnUrl chỉ hợp lệ khi role được phép vào path đó.
    /// VD: Lecturer không được redirect về /Users, /Packages, /Dashboard/Admin…
    /// </summary>
    private static bool IsAllowedReturnUrl(string returnUrl, string? role)
    {
        var path = returnUrl.Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(path)) path = "/";

        // Trang role-specific
        if (path.StartsWith("/dashboard/admin")) return role == "Admin";
        if (path.StartsWith("/dashboard/lecturer")) return role == "Lecturer";
        if (path.StartsWith("/dashboard/student")) return role == "Student";

        // AdminOnly
        string[] adminOnly =
        [
            "/users", "/allowedemails", "/reports", "/packages", "/auditlogs"
        ];
        if (adminOnly.Any(p => path == p || path.StartsWith(p + "/")))
            return role == "Admin";

        // LecturerOrAdmin
        string[] lecturerOrAdmin =
        [
            "/compare", "/dashboard/experiments", "/settings", "/subjects/chapters"
        ];
        if (lecturerOrAdmin.Any(p => path == p || path.StartsWith(p + "/")))
            return role is "Admin" or "Lecturer";

        // Còn lại: trang [Authorize] chung (Chat, Documents, Feedback…) — mọi role đã login đều được.
        return true;
    }
}
