using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface ISubjectRepository
{
    Task<List<Subject>> GetAllAsync();
    Task<Subject?> GetByIdAsync(string id);
    Task CreateAsync(Subject subject);
    Task UpdateAsync(Subject subject);
    Task DeleteAsync(string id);
    Task<long> CountAsync();
}



