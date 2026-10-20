using System.Globalization;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Domain.Modules.Settings;

namespace ByeMoney.Application.Modules.Purchases.Services;

public static class PurchaseReferenceRateReader
{
    // ظرفیت ستون numeric(18,0)؛ نرخ مرجع نامعتبر نباید مانع ثبت خرید شود.
    private const decimal MaximumReferenceRate = 999_999_999_999_999_999m;

    public static async Task<decimal?> ReadAsync(
        ISystemSettingRepository settings,
        CancellationToken cancellationToken)
    {
        var value = await settings.GetValueByKeyAsync(SystemSettingConstants.Keys.RialToNoor, cancellationToken);
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) ||
            rate <= 0 || rate > MaximumReferenceRate || decimal.Truncate(rate) != rate)
        {
            return null;
        }

        return rate;
    }
}
