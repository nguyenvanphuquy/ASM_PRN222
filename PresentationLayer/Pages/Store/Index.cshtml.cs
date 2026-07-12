using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Store;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IBillingService _billing;
    private readonly INotificationService _notifier;
    public IndexModel(IBillingService billing, INotificationService notifier)
    {
        _billing = billing;
        _notifier = notifier;
    }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Sinh viên";

    public List<Package> Packages { get; private set; } = new();
    public TokenBalance Balance { get; private set; } = new(0, 0, 0, null);
    public List<PackagePurchase> History { get; private set; } = new();
    public string Role { get; private set; } = "Student";

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Cửa hàng gói";
        ViewData["TopbarTitle"] = "🛒 Cửa hàng gói token";
        Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Student";

        // Sinh viên mới được cấp gói dùng thử để có số dư ban đầu.
        if (Role == "Student") await _billing.EnsureFreeGrantAsync(UserId);

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Packages = await _billing.GetPackagesAsync(activeOnly: true);
        Balance = await _billing.GetBalanceAsync(UserId);
        History = await _billing.GetUserPurchasesAsync(UserId);
    }

    public async Task<IActionResult> OnPostBuyAsync(string packageId)
    {
        var (ok, err, purchase) = await _billing.BuyAsync(UserId, packageId);
        TempData[ok ? "Success" : "Error"] = ok
            ? $"Mua {purchase!.PackageName} thành công! Đã cộng {purchase.TokensGranted:N0} token vào tài khoản."
            : err;
        if (ok) await _notifier.ActivityAsync("🛒", "Giao dịch", Actor, $"Mua gói \"{purchase!.PackageName}\" (+{purchase.TokensGranted:N0} token)");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(string purchaseId)
    {
        var (ok, err) = await _billing.CancelPurchaseAsync(UserId, purchaseId);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã hủy gói thành công." : err;
        if (ok) await _notifier.ActivityAsync("🚫", "Giao dịch", Actor, "Hủy một gói token");
        return RedirectToPage();
    }
}
