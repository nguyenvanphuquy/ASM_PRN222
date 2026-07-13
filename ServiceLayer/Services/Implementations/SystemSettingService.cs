using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace ServiceLayer.Services.Implementations;

public interface ISystemSettingService
{
    Task<string> GetSettingAsync(string key, string defaultValue);
    Task SetSettingAsync(string key, string value, string description = "");
}

public class SystemSettingService : ISystemSettingService
{
    private readonly AppDbContext _context;

    // AppDbContext là scoped và KHÔNG thread-safe. Các benchmark embed chunk song song
    // (EmbeddingComparisonService/ChunkingComparisonService) gọi GetSettingAsync đồng thời
    // qua cùng một instance này → nếu không khoá sẽ ném "A second operation was started on
    // this context instance". _gate tuần tự hoá mọi lần chạm DbContext; _cache (theo request)
    // tránh đọc lại DB cho cùng một key. Value null = "đã biết là không có trong DB".
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string?> _cache = new();

    public SystemSettingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_cache.TryGetValue(key, out var raw))
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
                raw = setting?.Value;
                _cache[key] = raw;
            }
            return raw ?? defaultValue;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetSettingAsync(string key, string value, string description = "")
    {
        await _gate.WaitAsync();
        try
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
            }
            else
            {
                setting.Value = value;
                if (!string.IsNullOrEmpty(description)) setting.Description = description;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            _cache[key] = value;
        }
        finally
        {
            _gate.Release();
        }
    }
}


