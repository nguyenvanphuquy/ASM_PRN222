using System.Text.RegularExpressions;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class AllowedEmailService : IAllowedEmailService
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IAllowedEmailRepository _repo;
    public AllowedEmailService(IAllowedEmailRepository repo) => _repo = repo;

    public Task<List<AllowedEmail>> GetAllAsync() => _repo.GetAllAsync();

    public async Task<(bool Ok, string? Error)> AddAsync(string email, string? note, string addedBy)
    {
        email = (email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return (false, "Email không hợp lệ.");
        if (await _repo.ExistsAsync(email))
            return (false, "Email này đã có trong danh sách.");

        await _repo.CreateAsync(new AllowedEmail
        {
            Email = email,
            Note = note?.Trim() ?? string.Empty,
            AddedBy = addedBy
        });
        return (true, null);
    }

    public Task DeleteAsync(string id) => _repo.DeleteAsync(id);

    public async Task<bool> IsAllowedAsync(string email)
    {
        // Whitelist trống = đăng ký mở cho mọi email (giữ tương thích hành vi cũ).
        if (await _repo.CountAsync() == 0) return true;
        return await _repo.ExistsAsync((email ?? string.Empty).Trim());
    }

    public async Task<bool> IsEnabledAsync() => await _repo.CountAsync() > 0;
}


