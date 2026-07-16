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
        // Hạn sử dụng = ngày XA NHẤT (toàn bộ token còn dùng được tới đó). Nếu có gói
        // không giới hạn thời gian (ExpiresAt = null) thì coi như không hết hạn.
        DateTime? validUntil = active.Any(p => p.ExpiresAt == null)
            ? null
            : active.Select(p => p.ExpiresAt).OrderByDescending(x => x).FirstOrDefault();
        return new TokenBalance(granted, used, Math.Max(0, granted - used), validUntil);
    }

    public Task<List<PackagePurchase>> GetUserPurchasesAsync(string userId) => _repo.GetUserPurchasesAsync(userId);

    public async Task<(bool, string?, PackagePurchase?)> BuyAsync(string userId, string packageId, string? idempotencyKey = null)
    {
        // Chống mua trùng: nếu key này đã tạo giao dịch (double-click / 2 request) thì trả về
        // giao dịch cũ, KHÔNG tạo thêm & KHÔNG cộng token lần nữa.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _repo.GetByIdempotencyKeyAsync(userId, idempotencyKey);
            if (existing != null) return (true, null, existing);
        }

        var pkg = await _repo.GetPackageAsync(packageId);
        if (pkg is null || !pkg.IsActive) return (false, "Gói không tồn tại hoặc đã ngừng bán", null);

        // Gói miễn phí (dùng thử) chỉ được CẤP TỰ ĐỘNG một lần (EnsureFreeGrantAsync), KHÔNG bán —
        // nếu cho mua thì sinh viên sẽ mua lặp để kéo dài hạn / gom token miễn phí.
        if (pkg.PriceVnd <= 0) return (false, "Gói dùng thử được cấp tự động, không thể mua.", null);

        var now = DateTime.UtcNow;

        // Mua thêm gói không chỉ CỘNG token mà còn KÉO DÀI hạn sử dụng: hạn mới =
        // (hạn xa nhất còn hiệu lực, hoặc hôm nay nếu đã hết) + số ngày của gói mới.
        DateTime? newExpiry = null;
        if (pkg.DurationDays > 0)
        {
            var active = await _repo.GetActivePaidPurchasesAsync(userId);
            var furthest = active.Where(p => p.ExpiresAt != null)
                                 .Select(p => p.ExpiresAt!.Value)
                                 .DefaultIfEmpty(now)
                                 .Max();
            var baseDate = furthest > now ? furthest : now;
            newExpiry = baseDate.AddDays(pkg.DurationDays);

            // Đưa các gói đang hiệu lực (chưa hủy) về chung hạn mới để toàn bộ token
            // dùng chung một hạn — không phần nào hết hạn sớm hơn.
            foreach (var p in active.Where(p => p.Status == "Paid" && p.ExpiresAt != null && p.ExpiresAt < newExpiry))
            {
                p.ExpiresAt = newExpiry;
                await _repo.UpdatePurchaseAsync(p);
            }
        }

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
            // Guid ⇒ mã giao dịch thực sự duy nhất (không còn trùng trong cùng 1 giây).
            TransactionRef = "TXN" + now.ToString("yyMMddHHmmss") + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            CreatedAt = now,
            ExpiresAt = newExpiry
        };

        try
        {
            await _repo.AddPurchaseAsync(purchase);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Va chạm unique IdempotencyKey ⇒ một request song song đã tạo giao dịch trước.
            // Trả về giao dịch đó (idempotent) thay vì báo lỗi/mua trùng.
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await _repo.GetByIdempotencyKeyAsync(userId, idempotencyKey);
                if (existing != null) return (true, null, existing);
            }
            return (false, "Không thể tạo giao dịch, vui lòng thử lại.", null);
        }

        // Realtime: báo cho user + cập nhật doanh thu trên trang báo cáo của admin.
        await _notifier.SendAsync(userId, "success", "Mua gói thành công",
            $"Bạn đã mua {pkg.Name} (+{pkg.TokenQuota:N0} token).");
        await _notifier.PackagePurchasedAsync(pkg.Name, pkg.PriceVnd, pkg.TokenQuota);

        var bal = await GetBalanceAsync(userId);
        await _notifier.TokenBalanceChangedAsync(userId, bal.Remaining, bal.Granted, bal.Used);

        return (true, null, purchase);
    }

    public async Task<(bool, string?)> CancelPurchaseAsync(string userId, string purchaseId)
    {
        var purchases = await _repo.GetUserPurchasesAsync(userId);
        var p = purchases.FirstOrDefault(x => x.Id == purchaseId);
        if (p is null) return (false, "Giao dịch không tồn tại hoặc không thuộc về bạn");
        if (p.Status != "Paid") return (false, "Gói này không ở trạng thái đang kích hoạt");

        // Hủy = ngừng gia hạn nhưng GIỮ QUYỀN DÙNG tới hết hạn (không thu hồi token ngay).
        // Gói vĩnh viễn (không có hạn) thì hủy = kết thúc ngay (đặt hạn = hiện tại).
        p.Status = "Cancelled";
        if (p.ExpiresAt == null) p.ExpiresAt = DateTime.UtcNow;
        await _repo.UpdatePurchaseAsync(p);

        var stillValid = p.ExpiresAt.HasValue && p.ExpiresAt.Value > DateTime.UtcNow;
        var msg = stillValid
            ? $"Đã hủy gói {p.PackageName}. Bạn vẫn dùng được số token còn lại tới hết hạn ({p.ExpiresAt!.Value.ToLocalTime():dd/MM/yyyy})."
            : $"Đã hủy gói {p.PackageName}.";
        await _notifier.SendAsync(userId, "warning", "Đã hủy gói", msg);
        var bal = await GetBalanceAsync(userId);
        await _notifier.TokenBalanceChangedAsync(userId, bal.Remaining, bal.Granted, bal.Used);
        return (true, msg);
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

    public Task DeductAsync(string userId, int tokens)
        // Trừ token nguyên tử (optimistic concurrency + cap chống tiêu lố) — xem BillingRepository.
        => _repo.DeductAsync(userId, tokens);

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
