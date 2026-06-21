using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IFeedbackRepository
{
    Task<List<Feedback>> GetAllAsync();
    Task<List<Feedback>> GetByUserAsync(string userId);
    Task<Feedback?> GetByIdAsync(string id);
    Task CreateAsync(Feedback feedback);
    Task UpdateAsync(Feedback feedback);
    Task DeleteAsync(string id);
    Task<int> CountAsync();
    Task<double> AverageRatingAsync();
}



