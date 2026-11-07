using ByeMoney.Application.Modules.Purchases.Orders.Constants;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class OrderIdFormat
{
    public static string For(OrderSourceType source, Guid id) =>
        $"{Prefix(source)}:{id:D}";

    public static bool IsValid(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var parts = value.Split(':');
        return parts.Length == 2 && parts[0] is "cart" or "topup" or "purchase" &&
            Guid.TryParseExact(parts[1], "D", out var id) && id != Guid.Empty &&
            parts[1] == id.ToString("D");
    }

    private static string Prefix(OrderSourceType source) => source switch
    {
        OrderSourceType.Cart => "cart",
        OrderSourceType.TopUpCheckout => "topup",
        OrderSourceType.Purchase => "purchase",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
