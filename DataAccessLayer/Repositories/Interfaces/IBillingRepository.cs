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
    /// <summary>Tìm giao dịch theo khoá chống mua trùng (idempotency). Null nếu chưa có.</summary>
    Task<PackagePurchase?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey);
    /// <summary>
    /// Trừ token FIFO NGUYÊN TỬ: gộp mọi thay đổi trong 1 SaveChanges (1 transaction),
    /// dùng optimistic concurrency (RowVersion). Xung đột với request song song thì tự nạp
    /// lại bản mới nhất và thử lại. KHÔNG cho TokensUsed vượt TokensGranted (không tiêu lố).
    /// </summary>
    Task DeductAsync(string userId, int tokens);

    // ── Bảo trì hạn dùng (job nền) ──
    /// <summary>Đánh dấu Expired cho các gói Paid đã quá hạn. Trả về số bản ghi đổi.</summary>
    Task<int> MarkExpiredAsync();
    /// <summary>Các gói Paid sắp hết hạn trong <paramref name="withinDays"/> ngày và CHƯA được nhắc.</summary>
    Task<List<PackagePurchase>> GetNearExpiryUnnotifiedAsync(int withinDays);
    /// <summary>Đánh dấu đã nhắc "sắp hết hạn" cho các giao dịch theo Id.</summary>
    Task MarkExpiryNotifiedAsync(IEnumerable<string> purchaseIds);

    // ── Token usage ──
    Task AddUsageAsync(TokenUsageLog log);
    Task<List<TokenUsageLog>> GetUsageSinceAsync(DateTime since);
    Task<List<TokenUsageLog>> GetUserUsageSinceAsync(string userId, DateTime since);
}
