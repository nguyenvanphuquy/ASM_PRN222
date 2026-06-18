using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IAllowedEmailRepository
{
    Task<List<AllowedEmail>> GetAllAsync();
    Task<bool> ExistsAsync(string email);
    Task<long> CountAsync();
    Task CreateAsync(AllowedEmail allowedEmail);
    Task DeleteAsync(string id);
}
