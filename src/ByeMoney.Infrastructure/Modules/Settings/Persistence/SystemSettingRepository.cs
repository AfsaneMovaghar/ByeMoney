using System.Globalization;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Domain.Modules.Settings;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Settings.Persistence;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly ApplicationDbContext _context;

    public SystemSettingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetRialToNoorConversionRateAsync(CancellationToken ct = default)
    {
        var setting = await _context.Set<SystemSetting>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SystemSettingConstants.Keys.RialToNoor, ct);

        if (setting is not null && decimal.TryParse(setting.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) && rate > 0)
        {
            return rate;
        }

        return SystemSettingConstants.Defaults.RialToNoorRate;
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.Set<SystemSetting>()
            .FirstOrDefaultAsync(s => s.Key == key, ct);
    }

    public async Task SetValueAsync(string key, string value, string? description = null, CancellationToken ct = default)
    {
        var setting = await GetByKeyAsync(key, ct);
        if (setting is null)
        {
            setting = SystemSetting.Create(key, value, description);
            await _context.Set<SystemSetting>().AddAsync(setting, ct);
        }
        else
        {
            setting.UpdateValue(value);
            _context.Set<SystemSetting>().Update(setting);
        }

        await _context.SaveChangesAsync(ct);
    }
}