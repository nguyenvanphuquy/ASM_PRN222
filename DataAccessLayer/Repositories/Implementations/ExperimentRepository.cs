using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories.Implementations;

public class ExperimentRepository : IExperimentRepository
{
    private readonly AppDbContext _db;
    public ExperimentRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ExperimentRun run)
    {
        _db.ExperimentRuns.Add(run);
        await _db.SaveChangesAsync();
    }

    public Task<List<ExperimentRun>> GetRecentAsync(int take = 50, string? kind = null)
    {
        var q = _db.ExperimentRuns.Include(r => r.Variants).AsQueryable();
        if (!string.IsNullOrEmpty(kind))
            q = q.Where(r => r.Kind == kind);
        return q.OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync();
    }

    public Task<List<ExperimentVariant>> GetVariantsSinceAsync(DateTime sinceUtc)
        => _db.ExperimentVariants
            .Include(v => v.Run)
            .Where(v => v.Run != null && v.Run.CreatedAt >= sinceUtc && !v.IsError)
            .ToListAsync();

    public async Task<int> CountAsync(string? kind = null)
    {
        var q = _db.ExperimentRuns.AsQueryable();
        if (!string.IsNullOrEmpty(kind))
            q = q.Where(r => r.Kind == kind);
        return await q.CountAsync();
    }

    public async Task<Dictionary<string, int>> GetCountsByKindAsync()
    {
        var rows = await _db.ExperimentRuns
            .GroupBy(r => r.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => x.Kind, x => x.Count);
    }
}
