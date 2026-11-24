namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// روش جمع‌آوری سراسری استرپی. ساخت مرسوله همین مقدار را می‌خواند و بدنهٔ درخواست ادمین آن را عوض نمی‌کند.
/// </summary>
public interface IShippingCollectionReader
{
    Task<ShippingCollectionRead> ReadAsync(CancellationToken ct);
}

public enum ShippingCollectionStatus
{
    Found,
    Unavailable
}

public sealed record ShippingCollectionRead(
    ShippingCollectionStatus Status,
    string? CollectionType,
    string? CourierCode = null,
    string? ServiceType = null)
{
    public static ShippingCollectionRead Found(
        string collectionType, string? courierCode = null, string? serviceType = null) =>
        new(ShippingCollectionStatus.Found, collectionType, courierCode, serviceType);

    public static ShippingCollectionRead Unavailable() =>
        new(ShippingCollectionStatus.Unavailable, null);
}

/// <summary>
/// کوریر و سرویس پیش‌فرض پستکس. تهی یا خارج از الگوی شناسه به پیش‌فرض برمی‌گردد.
/// </summary>
public static class ShippingDispatchCodes
{
    public const string DefaultCourier = "IR_POST";

    public const string DefaultServiceType = "EXPRESS";

    public static string NormalizeCourier(string? value) => Normalize(value, DefaultCourier);

    public static string NormalizeService(string? value) => Normalize(value, DefaultServiceType);

    private static string Normalize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (text.Length > 32 || !char.IsAsciiLetter(text[0]))
        {
            return fallback;
        }

        for (var i = 1; i < text.Length; i++)
        {
            var character = text[i];
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return fallback;
            }
        }

        return text.ToUpperInvariant();
    }
}

public static class ShippingCollectionTypes
{
    public const string PickUp = "pick_up";

    public const string CourierDropOff = "courier_drop_off";

    public const string PostexDropOff = "postex_drop_off";

    public const string Default = CourierDropOff;

    public static bool IsAllowed(string? value, out string normalized)
    {
        if (string.Equals(value?.Trim(), PickUp, StringComparison.OrdinalIgnoreCase))
        {
            normalized = PickUp;
            return true;
        }

        if (string.Equals(value?.Trim(), CourierDropOff, StringComparison.OrdinalIgnoreCase))
        {
            normalized = CourierDropOff;
            return true;
        }

        if (string.Equals(value?.Trim(), PostexDropOff, StringComparison.OrdinalIgnoreCase))
        {
            normalized = PostexDropOff;
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
