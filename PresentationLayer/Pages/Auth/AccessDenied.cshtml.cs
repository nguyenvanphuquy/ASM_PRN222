using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PresentationLayer.Helpers;

namespace PresentationLayer.Pages.Auth;

public class AccessDeniedModel : PageModel
{
    public IActionResult OnGet()
    {
        // Đã đăng nhập nhưng không đủ quyền: đưa thẳng về dashboard đúng role
        // (tránh đứng ở trang AccessDenied rồi phải bấm "Về trang chủ").
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(DashboardHome.PathFor(User));

        return RedirectToPage("/Auth/Login");
    }
}
