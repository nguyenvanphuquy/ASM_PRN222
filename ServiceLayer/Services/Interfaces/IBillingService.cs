using DataAccessLayer.Entities;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

/// <summary>
/// Quản lý gói token + thanh toán (giả lập) + số dư/tiêu thụ token của người dùng.
/// </summary>
public interface IBillingService
{
    // ── Gói (Admin quản lý) ──
    Task<List<Package>> GetPackagesAsync(bool activeOnly);
    Task<Package?> GetPackageAsync(string id);
    Task<(bool Success, string? Error)> CreatePackageAsync(Package p);
    Task<(bool Success, string? Error)> UpdatePackageAsync(Package p);
    Task<(bool Success, string? Error)> DeletePackageAsync(string id);
    Task EnsureSeedPackagesAsync();

    // ── Người dùng ──
    Task<TokenBalance> GetBalanceAsync(string userId);
    Task<List<PackagePurchase>> GetUserPurchasesAsync(string userId);
    /// <summary>Mua gói (thanh toán giả lập → cấp token ngay). <paramref name="idempotencyKey"/> chống mua trùng.</summary>
    Task<(bool Success, string? Error, PackagePurchase? Purchase)> BuyAsync(string userId, string packageId, string? idempotencyKey = null);
    /// <summary>Hủy một gói đã mua (trạng thái Paid).</summary>
    Task<(bool Success, string? Error)> CancelPurchaseAsync(string userId, string purchaseId);
    /// <summary>Cấp gói dùng thử miễn phí một lần cho người dùng chưa từng có giao dịch.</summary>
    Task EnsureFreeGrantAsync(string userId);
    Task<List<PackagePurchase>> GetAllPurchasesAsync();

    // ── Tiêu thụ token (ChatService gọi) ──
    Task<bool> HasQuotaAsync(string userId);
    Task DeductAsync(string userId, int tokens);
    /// <summary>Ghi nhật ký token cho một lần gọi LLM và (nếu meter=true) trừ vào quota.</summary>
    Task RecordUsageAsync(string userId, string? sessionId, LlmResult result, string kind, bool meter);
}
