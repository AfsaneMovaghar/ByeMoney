using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Services;

/// <summary>
/// اثر انگشت فهرست قابل تسویه (شناسه و سهم عادی و هدیهٔ نور خالص هر سفارش، به همان ترتیب پیش‌نمایش).
/// بستن دوره فقط وقتی انجام می‌شود که فهرست لحظهٔ بستن دقیقاً همانی باشد که ادمین دیده است؛
/// تطبیق تعداد و جمع کافی نیست، چون سفارش دیگری با همان مبلغ می‌تواند جای سفارش تسویه‌شده را بگیرد.
/// </summary>
public static class OrderSettlementPreviewToken
{
    public const int Length = 64;

    public static string Compute(IReadOnlyList<SettlementOrderResponse> orders)
    {
        var lines = orders.Select(order =>
            order.OrderId + "|" + Format(order.RegularNetAmountInNoor) + "|" + Format(order.GiftNetAmountInNoor));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

        return Convert.ToHexStringLower(hash);
    }

    private static string Format(decimal amount) =>
        amount.ToString("0.####", CultureInfo.InvariantCulture);
}
