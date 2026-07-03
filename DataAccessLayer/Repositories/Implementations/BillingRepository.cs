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
        var p = await _context.Packages.FindAsync(id);
        if (p != null)
        {
            _context.Packages.Remove(p);
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
        return _context.PackagePurchases
            .Where(p => p.UserId == userId
                        && p.Status == "Paid"
                        && (p.ExpiresAt == null || p.ExpiresAt > now))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public Task<List<PackagePurchase>> GetAllPurchasesAsync()
        => _context.PackagePurchases
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

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
