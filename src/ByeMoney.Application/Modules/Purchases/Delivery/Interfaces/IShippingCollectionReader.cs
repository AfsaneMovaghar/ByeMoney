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

public sealed record ShippingCollectionRead(ShippingCollectionStatus Status, string? CollectionType)
{
    public static ShippingCollectionRead Found(string collectionType) =>
        new(ShippingCollectionStatus.Found, collectionType);

    public static ShippingCollectionRead Unavailable() =>
        new(ShippingCollectionStatus.Unavailable, null);
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
