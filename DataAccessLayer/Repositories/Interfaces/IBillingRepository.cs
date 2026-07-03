using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories.Interfaces;

/// <summary>
/// Kho dữ liệu cho miền "kiếm tiền + tiêu thụ": Gói (Package), Giao dịch mua gói
/// (PackagePurchase) và Nhật ký token (TokenUsageLog).
/// </summary>
public interface IBillingRepository
{
    // ── Packages ──
    Task<List<Package>> GetPackagesAsync(bool activeOnly);
    Task<Package?> GetPackageAsync(string id);
    Task AddPackageAsync(Package p);
    Task UpdatePackageAsync(Package p);
    Task DeletePackageAsync(string id);
    Task<int> CountPackagesAsync();

    // ── Purchases ──
    Task AddPurchaseAsync(PackagePurchase p);
    Task UpdatePurchaseAsync(PackagePurchase p);
    Task<List<PackagePurchase>> GetUserPurchasesAsync(string userId);
    /// <summary>Các gói còn hiệu lực (Paid, chưa hết hạn) của user, sắp xếp cũ→mới để trừ token FIFO.</summary>
    Task<List<PackagePurchase>> GetActivePaidPurchasesAsync(string userId);
    Task<List<PackagePurchase>> GetAllPurchasesAsync();

    // ── Token usage ──
    Task AddUsageAsync(TokenUsageLog log);
    Task<List<TokenUsageLog>> GetUsageSinceAsync(DateTime since);
    Task<List<TokenUsageLog>> GetUserUsageSinceAsync(string userId, DateTime since);
}
