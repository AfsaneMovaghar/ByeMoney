using ByeMoney.Application.Modules.Purchases.Orders.Constants;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class OrderIdFormat
{
    public static string For(OrderSourceType source, Guid id) =>
        $"{Prefix(source)}:{id:D}";

    public static bool IsValid(string? value) => TryParse(value, out _, out _);

    /// <summary>شناسهٔ سفارش را به منبع و شناسهٔ خام تبدیل می‌کند؛ قالب نامعتبر false است.</summary>
    public static bool TryParse(string? value, out OrderSourceType source, out Guid id)
    {
        source = default;
        id = Guid.Empty;
        if (value is null)
        {
            return false;
        }

        var parts = value.Split(':');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[1], "D", out id) || id == Guid.Empty ||
            parts[1] != id.ToString("D"))
        {
            return false;
        }

        switch (parts[0])
        {
            case "cart":
                source = OrderSourceType.Cart;
                return true;
            case "topup":
                source = OrderSourceType.TopUpCheckout;
                return true;
            case "purchase":
                source = OrderSourceType.Purchase;
                return true;
            default:
                return false;
        }
    }

    private static string Prefix(OrderSourceType source) => source switch
    {
        OrderSourceType.Cart => "cart",
        OrderSourceType.TopUpCheckout => "topup",
        OrderSourceType.Purchase => "purchase",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
