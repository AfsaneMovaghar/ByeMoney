namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// درخواست ساخت مرسولهٔ سبد. <c>CollectionType</c> روش همین درخواست است:
/// <c>pick_up</c> (پیک بیاید) یا <c>courier_drop_off</c> (تحویل به باجهٔ پست).
/// <c>Provider</c> اختیاری است (مثلاً PostEx).
/// <c>BoxTypeId</c> اختیاری است (۱ تا ۱۲)؛ نبودش یعنی انتخاب خودکار جعبهٔ مرسوله.
/// </summary>
public sealed record CreatePhysicalShipmentApiRequest(
    string? CollectionType,
    string? Provider = null,
    int? BoxTypeId = null);
