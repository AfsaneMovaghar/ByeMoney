using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Delivery.Services;

/// <summary>
/// انتخاب واسط ساخت/خواندن مرسوله از بین پیاده‌سازی‌های پیکربندی‌شده.
/// پیش‌فرض پستکس است؛ واسط بعدی فقط یک ثبت تازه در DI می‌خواهد.
/// </summary>
public static class ShipmentClientSelector
{
    public static IShipmentProviderClient? Resolve(
        IEnumerable<IShipmentProviderClient> clients, string? provider)
    {
        var configured = clients
            .Where(item => item.IsConfigured && !string.IsNullOrWhiteSpace(item.Provider))
            .GroupBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            return configured.GetValueOrDefault(provider.Trim());
        }

        if (configured.TryGetValue(PhysicalProductPurchase.PostexProvider, out var postex))
        {
            return postex;
        }

        return configured.Values.FirstOrDefault();
    }
}
