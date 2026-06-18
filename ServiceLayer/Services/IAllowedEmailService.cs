using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IAllowedEmailService
{
    Task<List<AllowedEmail>> GetAllAsync();
    Task<(bool Ok, string? Error)> AddAsync(string email, string? note, string addedBy);
    Task DeleteAsync(string id);
    // True nếu email được phép đăng ký. Khi whitelist trống → mọi email đều được phép (mở).
    Task<bool> IsAllowedAsync(string email);
    // True nếu whitelist đang bật (có ít nhất 1 email).
    Task<bool> IsEnabledAsync();
}
