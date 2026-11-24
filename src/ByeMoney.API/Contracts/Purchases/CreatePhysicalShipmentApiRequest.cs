namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// درخواست ساخت مرسولهٔ سبد. روش جمع‌آوری از تنظیم سراسری استرپی خوانده می‌شود.
/// <c>Provider</c> اختیاری است (مثلاً PostEx).
/// <c>BoxTypeId</c> اختیاری است (۱ تا ۱۲)؛ نبودش یعنی انتخاب خودکار جعبهٔ مرسوله.
/// </summary>
public sealed record CreatePhysicalShipmentApiRequest(
    string? Provider = null,
    int? BoxTypeId = null);
