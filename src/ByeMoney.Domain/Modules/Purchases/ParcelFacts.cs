namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// عوامل فریزشدهٔ مرسوله برای یک ردیف کالا؛ وزن گرم کل ردیف، ابعاد سانتی‌متر و شناسهٔ جعبهٔ استاندارد پستکس.
/// مقادیر از prepare استرپی می‌آیند و در ساخت مرسوله دوباره محاسبه یا با پیش‌فرض جایگزین نمی‌شوند.
/// </summary>
public sealed record ParcelFacts(
    int WeightGrams,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    long DeclaredValueIrr,
    bool IsFragile,
    int BoxTypeId)
{
    public const int MinBoxTypeId = 1;
    public const int MaxBoxTypeId = 12;

    /// <summary>همهٔ عوامل برای ساخت مرسوله لازم‌اند؛ صفر یا منفی یعنی دادهٔ ناقص.</summary>
    public bool IsComplete =>
        WeightGrams > 0 &&
        LengthCm > 0 &&
        WidthCm > 0 &&
        HeightCm > 0 &&
        DeclaredValueIrr > 0 &&
        BoxTypeId >= MinBoxTypeId &&
        BoxTypeId <= MaxBoxTypeId;
}
