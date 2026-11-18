namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// درخواست ساخت مرسوله. <c>CollectionType</c> روش همین درخواست است:
/// <c>pick_up</c> (پیک بیاید) یا <c>courier_drop_off</c> (تحویل به باجهٔ پست).
/// <c>Provider</c> اختیاری است (مثلاً PostEx).
/// </summary>
public sealed record CreatePhysicalShipmentApiRequest(string? CollectionType, string? Provider = null);
