namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// عوامل فریزشدهٔ مرسوله برای یک ردیف کالا؛ وزن گرم کل ردیف و ابعاد سانتی‌متر.
/// مقادیر از prepare استرپی می‌آیند و در ساخت مرسوله دوباره محاسبه یا با پیش‌فرض جایگزین نمی‌شوند.
/// </summary>
public sealed record ParcelFacts(
    int WeightGrams,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    long DeclaredValueIrr,
    bool IsFragile)
{
    /// <summary>همهٔ عوامل برای ساخت مرسوله لازم‌اند؛ صفر یا منفی یعنی دادهٔ ناقص.</summary>
    public bool IsComplete =>
        WeightGrams > 0 &&
        LengthCm > 0 &&
        WidthCm > 0 &&
        HeightCm > 0 &&
        DeclaredValueIrr > 0;
}
