using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
            return RedirectToPage("/Dashboard/Index");
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId!),
            new(ClaimTypes.Name,           result.Username!),
            new("FullName",                result.FullName ?? result.Username!),
            new(ClaimTypes.Role,           result.Role ?? "Student"),
            new("CanUpload",               (result.Role == "Admin" || result.Role == "Lecturer" || result.CanUploadDocuments) ? "true" : "false")
        };

        if (!string.IsNullOrEmpty(result.AvatarPath))
            claims.Add(new("AvatarPath", result.AvatarPath));
        var assigned = result.AssignedSubjectIds ?? Array.Empty<string>();
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

        return LocalRedirect(returnUrl ?? "/Dashboard");
    }
}
