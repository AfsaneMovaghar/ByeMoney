using System.Globalization;

namespace ByeMoney.Application.Modules.Purchases.Carts.Constants;

public static class CartErrors
{
    private static string Read(string key) => Resources.ApplicationErrors.ResourceManager.GetString(key)!;
    public static string Invalid => Read("Cart_Invalid");
    public static string Conflict => Read("Cart_Conflict");
    public static string OriginCityMismatch => Read("Cart_OriginCityMismatch");
    public static string NotFound => Read("Cart_NotFound");
    public static string InsufficientBalance => Read("Cart_InsufficientBalance");
    public static string Unavailable => Read("Cart_Unavailable");
    public static string OutOfStock => Read("Cart_OutOfStock");

    public static string InsufficientStock(int availableQuantity) =>
        string.Format(CultureInfo.InvariantCulture, Read("Cart_InsufficientStock"), availableQuantity);
}
