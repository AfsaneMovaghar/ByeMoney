using ByeMoney.Domain.Modules.Settings;

namespace ByeMoney.Application.Modules.Settings;

public interface ISystemSettingRepository
{
    Task<decimal> GetRialToNoorConversionRateAsync(CancellationToken ct = default);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task SetValueAsync(string key, string value, string? description = null, CancellationToken ct = default);
}
