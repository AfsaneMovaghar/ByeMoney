namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// مبدأ فریزشدهٔ سبد برای نمایش. <c>Label</c> در سفارش قدیمی می‌تواند خالی باشد.
/// </summary>
public sealed record ShippingOriginView(string Id, string? Label);
