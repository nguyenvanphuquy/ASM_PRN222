using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class BillingService : IBillingService
{
    private readonly IBillingRepository _repo;
    private readonly INotificationService _notifier;

    public BillingService(IBillingRepository repo, INotificationService notifier)
    {
        _repo = repo;
        _notifier = notifier;
    }

    // ── Packages ──
    public Task<List<Package>> GetPackagesAsync(bool activeOnly) => _repo.GetPackagesAsync(activeOnly);
    public Task<Package?> GetPackageAsync(string id) => _repo.GetPackageAsync(id);

    public async Task<(bool, string?)> CreatePackageAsync(Package p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) return (false, "Tên gói bắt buộc");
        if (p.TokenQuota <= 0) return (false, "Số token phải lớn hơn 0");
        if (p.PriceVnd < 0) return (false, "Giá không hợp lệ");
        await _repo.AddPackageAsync(p);
        await _notifier.PackagesChangedAsync("created");
        return (true, null);
    }

    public async Task<(bool, string?)> UpdatePackageAsync(Package p)
    {
        var existing = await _repo.GetPackageAsync(p.Id);
        if (existing is null) return (false, "Gói không tồn tại");
        existing.Name = p.Name;
        existing.Description = p.Description;
        existing.PriceVnd = p.PriceVnd;
        existing.TokenQuota = p.TokenQuota;
        existing.DurationDays = p.DurationDays;
        existing.IsActive = p.IsActive;
        existing.IsPopular = p.IsPopular;
        await _repo.UpdatePackageAsync(existing);
        await _notifier.PackagesChangedAsync("updated");
        return (true, null);
    }

    public async Task<(bool, string?)> DeletePackageAsync(string id)
    {
        var existing = await _repo.GetPackageAsync(id);
        if (existing is null) return (false, "Gói không tồn tại");
        await _repo.DeletePackageAsync(id);
        await _notifier.PackagesChangedAsync("deleted");
        return (true, null);
    }

    public async Task EnsureSeedPackagesAsync()
    {
        if (await _repo.CountPackagesAsync() > 0) return;

        var seeds = new List<Package>
        {
            new() { Name = "Gói Dùng Thử",   Description = "Miễn phí cho người dùng mới — trải nghiệm hỏi đáp AI.", PriceVnd = 0,      TokenQuota = 20_000,    DurationDays = 30 },
            new() { Name = "Gói Cơ Bản",     Description = "Đủ dùng cho nhu cầu ôn tập hằng tuần.",                 PriceVnd = 49_000,  TokenQuota = 200_000,   DurationDays = 30 },
            new() { Name = "Gói Nâng Cao",   Description = "Hỏi đáp thoải mái cả kỳ, kèm so sánh model.",           PriceVnd = 149_000, TokenQuota = 800_000,   DurationDays = 30, IsPopular = true },
            new() { Name = "Gói Chuyên Nghiệp", Description = "Dành cho nhóm học tập / dùng cường độ cao.",          PriceVnd = 399_000, TokenQuota = 3_000_000, DurationDays = 90 },
        };
        foreach (var p in seeds) await _repo.AddPackageAsync(p);
    }

    // ── Balance / purchase ──
    public async Task<TokenBalance> GetBalanceAsync(string userId)
    {
        var active = await _repo.GetActivePaidPurchasesAsync(userId);
        int granted = active.Sum(p => p.TokensGranted);
        int used = active.Sum(p => p.TokensUsed);
        var nextExpiry = active.Where(p => p.ExpiresAt != null).OrderBy(p => p.ExpiresAt).FirstOrDefault()?.ExpiresAt;
        return new TokenBalance(granted, used, Math.Max(0, granted - used), nextExpiry);
    }

    public Task<List<PackagePurchase>> GetUserPurchasesAsync(string userId) => _repo.GetUserPurchasesAsync(userId);

    public async Task<(bool, string?, PackagePurchase?)> BuyAsync(string userId, string packageId)
    {
        var pkg = await _repo.GetPackageAsync(packageId);
        if (pkg is null || !pkg.IsActive) return (false, "Gói không tồn tại hoặc đã ngừng bán", null);

        var purchase = new PackagePurchase
        {
            UserId = userId,
            PackageId = pkg.Id,
            PackageName = pkg.Name,
            AmountVnd = pkg.PriceVnd,
            TokensGranted = pkg.TokenQuota,
            TokensUsed = 0,
            Status = "Paid",
            PaymentMethod = "Mock",
            TransactionRef = "TXN" + DateTime.UtcNow.ToString("yyMMddHHmmss") + Random.Shared.Next(100, 999),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = pkg.DurationDays > 0 ? DateTime.UtcNow.AddDays(pkg.DurationDays) : null
        };
        await _repo.AddPurchaseAsync(purchase);

        // Realtime: báo cho user + cập nhật doanh thu trên trang báo cáo của admin.
        await _notifier.SendAsync(userId, "success", "Mua gói thành công",
            $"Bạn đã mua {pkg.Name} (+{pkg.TokenQuota:N0} token).");
        await _notifier.PackagePurchasedAsync(pkg.Name, pkg.PriceVnd, pkg.TokenQuota);

        return (true, null, purchase);
    }

    public async Task<(bool, string?)> CancelPurchaseAsync(string userId, string purchaseId)
    {
        var purchases = await _repo.GetUserPurchasesAsync(userId);
        var p = purchases.FirstOrDefault(x => x.Id == purchaseId);
        if (p is null) return (false, "Giao dịch không tồn tại hoặc không thuộc về bạn");
        if (p.Status != "Paid") return (false, "Gói này không ở trạng thái đang kích hoạt");

        p.Status = "Cancelled";
        await _repo.UpdatePurchaseAsync(p);

        await _notifier.SendAsync(userId, "warning", "Đã hủy gói", $"Gói {p.PackageName} của bạn đã bị hủy.");
        return (true, null);
    }

    public Task<List<PackagePurchase>> GetAllPurchasesAsync() => _repo.GetAllPurchasesAsync();

    public async Task EnsureFreeGrantAsync(string userId)
    {
        var purchases = await _repo.GetUserPurchasesAsync(userId);
        if (purchases.Count > 0) return; // đã có giao dịch → không cấp lại

        // Ưu tiên gói giá 0 trong danh mục; nếu không có thì cấp mặc định 20k token.
        var freePkg = (await _repo.GetPackagesAsync(true)).FirstOrDefault(p => p.PriceVnd == 0);
        var purchase = new PackagePurchase
        {
            UserId = userId,
            PackageId = freePkg?.Id ?? "free",
            PackageName = freePkg?.Name ?? "Gói Dùng Thử",
            AmountVnd = 0,
            TokensGranted = freePkg?.TokenQuota ?? 20_000,
            TokensUsed = 0,
            Status = "Paid",
            PaymentMethod = "Free",
            TransactionRef = "FREE" + DateTime.UtcNow.ToString("yyMMddHHmmss"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(freePkg?.DurationDays > 0 ? freePkg.DurationDays : 30)
        };
        await _repo.AddPurchaseAsync(purchase);
    }

    // ── Consumption ──
    public async Task<bool> HasQuotaAsync(string userId)
    {
        var bal = await GetBalanceAsync(userId);
        return bal.Remaining > 0;
    }

    public async Task DeductAsync(string userId, int tokens)
    {
        if (tokens <= 0) return;
        var active = await _repo.GetActivePaidPurchasesAsync(userId); // FIFO (cũ → mới)
        int remaining = tokens;
        foreach (var p in active)
        {
            if (remaining <= 0) break;
            int free = p.TokensGranted - p.TokensUsed;
            if (free <= 0) continue;
            int take = Math.Min(free, remaining);
            p.TokensUsed += take;
            remaining -= take;
            await _repo.UpdatePurchaseAsync(p);
        }
        // Nếu vẫn còn dư (vượt quota) → dồn hết vào bản ghi cuối để phản ánh đúng lượng đã tiêu.
        if (remaining > 0 && active.Count > 0)
        {
            var last = active[^1];
            last.TokensUsed += remaining;
            await _repo.UpdatePurchaseAsync(last);
        }
    }

    public async Task RecordUsageAsync(string userId, string? sessionId, LlmResult result, string kind, bool meter)
    {
        var log = new TokenUsageLog
        {
            UserId = userId,
            SessionId = sessionId,
            Model = result.Model,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            TotalTokens = result.TotalTokens,
            CostUsd = ModelCatalog.EstimateCostUsd(result.Model, result.PromptTokens, result.CompletionTokens),
            Kind = kind
        };
        await _repo.AddUsageAsync(log);

        if (meter && result.TotalTokens > 0)
            await DeductAsync(userId, result.TotalTokens);
    }
}
