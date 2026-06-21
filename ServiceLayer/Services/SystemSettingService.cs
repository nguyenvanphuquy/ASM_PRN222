using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace ServiceLayer.Services;

public interface ISystemSettingService
{
    Task<string> GetSettingAsync(string key, string defaultValue);
    Task SetSettingAsync(string key, string value, string description = "");
}

public class SystemSettingService : ISystemSettingService
{
    private readonly AppDbContext _context;

    public SystemSettingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    public async Task SetSettingAsync(string key, string value, string description = "")
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
    }
}
