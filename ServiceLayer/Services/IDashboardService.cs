using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync();
}
