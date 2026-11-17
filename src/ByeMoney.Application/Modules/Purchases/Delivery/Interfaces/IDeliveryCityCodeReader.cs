namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// خواندن کد شهر حامل از جدول محل. کد در بای‌مانی ذخیره نمی‌شود.
/// اگر شناسهٔ شهر باشد فقط همان خوانده می‌شود؛ نبود شناسه (سفارش قدیمی) فقط نام دقیق استان و شهر است.
/// </summary>
public interface IDeliveryCityCodeReader
{
    Task<DeliveryCityCodeRead> ReadAsync(
        string? cityDocumentId,
        string province,
        string city,
        CancellationToken ct);
}

public enum DeliveryCityCodeStatus
{
    Found,
    NotFound,
    Unavailable
}

public sealed record DeliveryCityCodeRead(DeliveryCityCodeStatus Status, int? Code)
{
    public static DeliveryCityCodeRead Found(int code) => new(DeliveryCityCodeStatus.Found, code);

    public static DeliveryCityCodeRead NotFound() => new(DeliveryCityCodeStatus.NotFound, null);

    public static DeliveryCityCodeRead Unavailable() => new(DeliveryCityCodeStatus.Unavailable, null);
}
