using System.Security.Claims;

namespace PresentationLayer.Helpers;

/// <summary>Đường dẫn trang chủ Dashboard theo vai trò.</summary>
public static class DashboardHome
{
    public static string PathFor(string? role) => role switch
    {
        "Admin" => "/Dashboard/Admin",
        "Lecturer" => "/Dashboard/Lecturer",
        _ => "/Dashboard/Student"
    };

    public static string PageFor(string? role) => role switch
    {
        "Admin" => "/Dashboard/Admin",
        "Lecturer" => "/Dashboard/Lecturer",
        _ => "/Dashboard/Student"
    };

    public static string PathFor(ClaimsPrincipal user)
        => PathFor(user.FindFirst(ClaimTypes.Role)?.Value);

    public static string PageFor(ClaimsPrincipal user)
        => PageFor(user.FindFirst(ClaimTypes.Role)?.Value);
}
