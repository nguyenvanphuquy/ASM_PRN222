using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories.Implementations;

public class BillingRepository : IBillingRepository
{
    private readonly AppDbContext _context;
    public BillingRepository(AppDbContext context) => _context = context;

    // ── Packages ──
    public Task<List<Package>> GetPackagesAsync(bool activeOnly)
        => _context.Packages
            .Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.PriceVnd)
            .ToListAsync();

    public Task<Package?> GetPackageAsync(string id)
        => _context.Packages.FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddPackageAsync(Package p)
    {
        _context.Packages.Add(p);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePackageAsync(Package p)
    {
        _context.Packages.Update(p);
        await _context.SaveChangesAsync();
    }

    public async Task DeletePackageAsync(string id)
    {
        // Xoá mềm: giữ lại row để lịch sử mua (PackagePurchases) không mồ côi khoá ngoại.
        // Gói bị đánh dấu IsDeleted sẽ bị query filter ẩn khỏi cửa hàng & trang quản trị.
        var p = await _context.Packages.FindAsync(id);
        if (p != null && !p.IsDeleted)
        {
            p.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }

    public Task<int> CountPackagesAsync() => _context.Packages.CountAsync();

    // ── Purchases ──
    public async Task AddPurchaseAsync(PackagePurchase p)
    {
        _context.PackagePurchases.Add(p);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePurchaseAsync(PackagePurchase p)
    {
        _context.PackagePurchases.Update(p);
        await _context.SaveChangesAsync();
    }

    public Task<List<PackagePurchase>> GetUserPurchasesAsync(string userId)
        => _context.PackagePurchases
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<List<PackagePurchase>> GetActivePaidPurchasesAsync(string userId)
    {
        var now = DateTime.UtcNow;
        // Gói còn dùng được = đã thanh toán VÀ chưa hết hạn. Gói đã HỦY vẫn được dùng
        // cho tới khi hết hạn (giống cơ chế subscription: hủy nhưng giữ quyền tới cuối kỳ).
        return _context.PackagePurchases
            .Where(p => p.UserId == userId
                        && (p.Status == "Paid" || p.Status == "Cancelled")
                        && (p.ExpiresAt == null || p.ExpiresAt > now))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public Task<List<PackagePurchase>> GetAllPurchasesAsync()
        => _context.PackagePurchases
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<PackagePurchase?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey)
        => _context.PackagePurchases
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IdempotencyKey == idempotencyKey);

    public async Task DeductAsync(string userId, int tokens)
    {
        if (tokens <= 0) return;
        var now = DateTime.UtcNow;

        // Trừ token NGUYÊN TỬ bằng pessimistic row lock: khóa (UPDLOCK) các dòng gói còn hiệu
        // lực của user trong 1 transaction. Hai request song song của cùng user sẽ XẾP HÀNG tại
        // đây rồi trừ tuần tự trên dữ liệu mới nhất ⇒ không mất update, không cần retry.
        await using var tx = await _context.Database.BeginTransactionAsync();

        var active = await _context.PackagePurchases
            .FromSqlInterpolated($@"SELECT * FROM PackagePurchases WITH (UPDLOCK, ROWLOCK)
                WHERE UserId = {userId}
                  AND (Status = 'Paid' OR Status = 'Cancelled')
                  AND (ExpiresAt IS NULL OR ExpiresAt > {now})")
            .OrderBy(p => p.CreatedAt) // FIFO cũ → mới
            .ToListAsync();

        var remaining = tokens;
        foreach (var p in active)
        {
            if (remaining <= 0) break;
            var free = p.TokensGranted - p.TokensUsed;
            if (free <= 0) continue;
            var take = Math.Min(free, remaining);
            p.TokensUsed += take;
            remaining -= take;
        }
        // remaining > 0 ⇒ câu vượt hạn mức (câu cuối): KHÔNG dồn phần dư vào đâu ⇒ TokensUsed
        // không bao giờ vượt TokensGranted (không "tiêu lố"), số dư sàn 0. Lần sau HasQuota=0 chặn.

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public Task<int> MarkExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return _context.PackagePurchases
            .Where(p => p.Status == "Paid" && p.ExpiresAt != null && p.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Expired"));
    }

    public Task<List<PackagePurchase>> GetNearExpiryUnnotifiedAsync(int withinDays)
    {
        var now = DateTime.UtcNow;
        var limit = now.AddDays(withinDays);
        return _context.PackagePurchases
            .Where(p => p.Status == "Paid" && !p.ExpiryNotified
                        && p.ExpiresAt != null && p.ExpiresAt > now && p.ExpiresAt <= limit)
            .ToListAsync();
    }

    public async Task MarkExpiryNotifiedAsync(IEnumerable<string> purchaseIds)
    {
        var ids = purchaseIds.ToList();
        if (ids.Count == 0) return;
        await _context.PackagePurchases
            .Where(p => ids.Contains(p.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiryNotified, true));
    }

    // ── Token usage ──
    public async Task AddUsageAsync(TokenUsageLog log)
    {
        _context.TokenUsageLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public Task<List<TokenUsageLog>> GetUsageSinceAsync(DateTime since)
        => _context.TokenUsageLogs
            .Where(l => l.CreatedAt >= since)
            .ToListAsync();

    public Task<List<TokenUsageLog>> GetUserUsageSinceAsync(string userId, DateTime since)
        => _context.TokenUsageLogs
            .Where(l => l.UserId == userId && l.CreatedAt >= since)
            .ToListAsync();
}
