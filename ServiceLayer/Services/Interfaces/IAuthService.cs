using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password);
    Task EnsureSeedUsersAsync();
}


