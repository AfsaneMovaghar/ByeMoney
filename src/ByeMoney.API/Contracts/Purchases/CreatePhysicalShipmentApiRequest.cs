namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// درخواست ساخت مرسوله؛ <c>Provider</c> اختیاری است (مثلاً PostEx).
/// در نبود آن، اولین واسط ساخت مرسولهٔ پیکربندی‌شده انتخاب می‌شود.
/// </summary>
public sealed record CreatePhysicalShipmentApiRequest(string? Provider = null);
