using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IChapterRepository
{
    Task<List<Chapter>> GetBySubjectAsync(string subjectId);
    Task<List<Chapter>> GetAllAsync();
    Task<Chapter?> GetByIdAsync(string id);
    Task CreateAsync(Chapter chapter);
    Task UpdateAsync(Chapter chapter);
    Task DeleteAsync(string id);
    Task<int> CountBySubjectAsync(string subjectId);
}



