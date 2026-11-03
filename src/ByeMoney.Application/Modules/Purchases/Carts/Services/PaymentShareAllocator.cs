namespace ByeMoney.Application.Modules.Purchases.Carts.Services;

/// <summary>
/// تقسیم نور هدیهٔ مصرف‌شده در یک پرداخت بین بخش‌های آن (اقلام و سپس ارسال)؛ جمع سهم‌ها دقیقاً برابر هدیهٔ مصرف‌شده است.
/// </summary>
public static class PaymentShareAllocator
{
    public static decimal[] SplitGift(decimal giftDebited, IReadOnlyList<decimal> amounts)
    {
        var shares = new decimal[amounts.Count];
        var total = amounts.Sum();
        if (giftDebited <= 0 || total <= 0)
        {
            return shares;
        }

        for (var i = 0; i < amounts.Count; i++)
        {
            shares[i] = decimal.Floor(giftDebited * amounts[i] / total);
        }

        // باقی‌ماندهٔ گردکردن از آخرین بخش به عقب و فقط تا ظرفیت همان بخش پخش می‌شود.
        var remainder = giftDebited - shares.Sum();
        for (var i = amounts.Count - 1; i >= 0 && remainder > 0; i--)
        {
            var added = Math.Min(remainder, amounts[i] - shares[i]);
            shares[i] += added;
            remainder -= added;
        }

        return shares;
    }
}
