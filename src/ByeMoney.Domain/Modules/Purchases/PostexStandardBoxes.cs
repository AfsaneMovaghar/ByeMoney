namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// ابعاد جعبه‌های استاندارد پستکس، شناسهٔ ۱ تا ۱۲ (طول × عرض × ارتفاع، سانتی‌متر).
/// شناسهٔ ۱۳ ابعاد ندارد و پذیرفته نمی‌شود. منبع جدول، بند ۹.۴ سند پستکس است.
/// </summary>
public static class PostexStandardBoxes
{
    public readonly record struct Size(int LengthCm, int WidthCm, int HeightCm);

    private static readonly Size[] Sizes =
    [
        default,
        new(22, 11, 2),
        new(31, 22, 2),
        new(45, 30, 2),
        new(10, 10, 15),
        new(15, 10, 20),
        new(20, 15, 20),
        new(20, 20, 30),
        new(25, 25, 35),
        new(25, 20, 45),
        new(30, 25, 40),
        new(40, 30, 45),
        new(45, 35, 50),
    ];

    public static bool TryGet(int boxTypeId, out Size size)
    {
        if (boxTypeId < ParcelFacts.MinBoxTypeId || boxTypeId > ParcelFacts.MaxBoxTypeId)
        {
            size = default;
            return false;
        }

        size = Sizes[boxTypeId];
        return true;
    }

    /// <summary>
    /// جعبهٔ مرسولهٔ سبد. پایه بزرگ‌ترین شناسهٔ اقلام است.
    /// شناسهٔ ۴ تا ۱۱ با بیش از یک واحد یک پله بزرگ می‌شود و از ۱۲ بالاتر نمی‌رود.
    /// پاکت‌های ۱ تا ۳ افزایش نمی‌گیرند. شناسهٔ دستی بدون افزایش برمی‌گردد.
    /// </summary>
    public static int ShipmentBoxTypeId(int baseBoxTypeId, int unitCount, int? manualBoxTypeId)
    {
        if (manualBoxTypeId is int manual)
        {
            return manual;
        }

        if (baseBoxTypeId is >= 4 and <= 11 && unitCount > 1)
        {
            return baseBoxTypeId + 1;
        }

        return baseBoxTypeId;
    }
}
