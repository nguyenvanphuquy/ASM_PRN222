using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories.Interfaces;

public interface IExperimentRepository
{
    Task AddAsync(ExperimentRun run);
    Task<List<ExperimentRun>> GetRecentAsync(int take = 50, string? kind = null);
    Task<List<ExperimentVariant>> GetVariantsSinceAsync(DateTime sinceUtc);
    Task<int> CountAsync(string? kind = null);
    Task<Dictionary<string, int>> GetCountsByKindAsync();
}
