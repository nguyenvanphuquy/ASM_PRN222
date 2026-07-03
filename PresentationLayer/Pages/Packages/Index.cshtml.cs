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
    public IndexModel(IBillingService billing) => _billing = billing;

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
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (ok, err) = await _billing.DeletePackageAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xoá gói." : err;
        return RedirectToPage();
    }
}
