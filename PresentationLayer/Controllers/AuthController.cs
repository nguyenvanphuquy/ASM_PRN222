using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API xác thực tài khoản (Auth) — Đăng nhập, đăng ký, đăng xuất, kích hoạt email.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>Đăng nhập vào hệ thống (lưu session cookie).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await _authService.LoginAsync(req.Username, req.Password);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Đăng nhập thất bại." });
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
        if (!string.IsNullOrEmpty(result.AssignedSubjectId))
            claims.Add(new("AssignedSubjectId", result.AssignedSubjectId));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        return Ok(new
        {
            message = "Đăng nhập thành công.",
            user = new
            {
                id = result.UserId,
                username = result.Username,
                fullName = result.FullName,
                role = result.Role,
                avatarPath = result.AvatarPath
            }
        });
    }

    /// <summary>Đăng ký tài khoản mới cho Sinh viên.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await _authService.RegisterAsync(req.Username, req.Email, req.Password, req.FullName);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Đăng ký thất bại." });
        }

        return Ok(new { message = "Đăng ký tài khoản thành công. Tài khoản của bạn đã được kích hoạt mặc định." });
    }

    /// <summary>Kích hoạt tài khoản qua token email.</summary>
    [HttpGet("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Token xác thực không hợp lệ." });

        var (success, err) = await _userService.VerifyEmailAsync(token);
        if (!success)
        {
            return BadRequest(new { message = err ?? "Kích hoạt tài khoản thất bại." });
        }

        return Ok(new { message = "Kích hoạt tài khoản thành công. Bạn hiện có thể đăng nhập." });
    }

    /// <summary>Đăng xuất khỏi hệ thống.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Đăng xuất thành công." });
    }
}
