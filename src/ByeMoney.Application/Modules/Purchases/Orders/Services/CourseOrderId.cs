using ByeMoney.Application.Modules.Purchases.Orders.Constants;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class CourseOrderId
{
    public static string For(CourseOrderSourceType source, Guid id) =>
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

    private static string Prefix(CourseOrderSourceType source) => source switch
    {
        CourseOrderSourceType.Cart => "cart",
        CourseOrderSourceType.TopUpCheckout => "topup",
        CourseOrderSourceType.Purchase => "purchase",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
