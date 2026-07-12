using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Packages;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IBillingService _billing;
    private readonly INotificationService _notifier;
    public IndexModel(IBillingService billing, INotificationService notifier)
    {
        _billing = billing;
        _notifier = notifier;
    }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Quản trị viên";

    public List<Package> Packages { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Quản lý gói";
        ViewData["TopbarTitle"] = "🏷️ Quản lý gói token";
        Packages = await _billing.GetPackagesAsync(activeOnly: false);
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, string? description, long priceVnd, int tokenQuota, int durationDays, bool isPopular)
    {
        var (ok, err) = await _billing.CreatePackageAsync(new Package
        {
            Name = name?.Trim() ?? "",
            Description = description?.Trim() ?? "",
            PriceVnd = priceVnd,
            TokenQuota = tokenQuota,
            DurationDays = durationDays,
            IsPopular = isPopular,
            IsActive = true
        });
        TempData[ok ? "Success" : "Error"] = ok ? "Đã tạo gói mới." : err;
        if (ok) await _notifier.ActivityAsync("📦", "Gói", Actor, $"Tạo gói \"{name}\" ({priceVnd:N0}đ · {tokenQuota:N0} token)");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(string id, string name, string? description, long priceVnd, int tokenQuota, int durationDays, bool isPopular, bool isActive)
    {
        var (ok, err) = await _billing.UpdatePackageAsync(new Package
        {
            Id = id,
            Name = name?.Trim() ?? "",
            Description = description?.Trim() ?? "",
            PriceVnd = priceVnd,
            TokenQuota = tokenQuota,
            DurationDays = durationDays,
            IsPopular = isPopular,
            IsActive = isActive
        });
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật gói." : err;
        if (ok) await _notifier.ActivityAsync("📦", "Gói", Actor, $"Cập nhật gói \"{name}\" ({priceVnd:N0}đ · {tokenQuota:N0} token · {(isActive ? "đang bán" : "ngừng bán")})");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var name = (await _billing.GetPackageAsync(id))?.Name ?? id;
        var (ok, err) = await _billing.DeletePackageAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xoá gói." : err;
        if (ok) await _notifier.ActivityAsync("📦", "Gói", Actor, $"Xoá gói \"{name}\"");
        return RedirectToPage();
    }
}
