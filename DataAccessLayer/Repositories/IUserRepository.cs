using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByVerificationTokenAsync(string token);
    Task<List<User>> GetAllAsync();
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string id);
    Task<long> CountAsync();
    Task<long> CountByRoleAsync(string role);
}



