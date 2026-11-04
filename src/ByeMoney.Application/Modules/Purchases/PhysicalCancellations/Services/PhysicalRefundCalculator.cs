using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

/// <summary>
/// محاسبهٔ برگشت لغو تعدادی از یک قلم طبق D09: مبلغ هر واحد گرد به پایین است و باقیماندهٔ گردکردن
/// به آخرین واحد می‌رسد (۳۳، ۳۳، ۳۴ از ۱۰۰). جمع برگشت همهٔ واحدها دقیقاً برابر سهم عادی و هدیهٔ پرداخت است.
/// </summary>
public static class PhysicalRefundCalculator
{
    public static PaymentShares RefundFor(PhysicalProductPurchase purchase, int quantity)
    {
        var regularPaid = purchase.RegularAmountInNoor!.Value;
        var giftPaid = purchase.GiftAmountInNoor!.Value;
        var regularLeft = regularPaid - purchase.RefundedRegularInNoor;
        var giftLeft = giftPaid - purchase.RefundedGiftInNoor;
        var cancelledAfter = purchase.CancelledQuantity + quantity;

        if (cancelledAfter == purchase.Quantity)
        {
            return new PaymentShares(regularLeft, giftLeft);
        }

        // مبلغ تجمعی برگشت پس از این لغو، از پرداخت واقعی و به نسبت تعداد، گرد به پایین.
        var refundedSoFar = purchase.RefundedRegularInNoor + purchase.RefundedGiftInNoor;
        var amount = CumulativeShare(purchase.PaidAmountInNoor, cancelledAfter, purchase.Quantity) - refundedSoFar;

        // سهم هدیه به همان نسبت است، ولی طوری محدود می‌شود که هیچ نوع نور از سهم باقی‌ماندهٔ خودش بیشتر برنگردد.
        var giftTarget = CumulativeShare(giftPaid, cancelledAfter, purchase.Quantity) - purchase.RefundedGiftInNoor;
        var minimumGift = Math.Max(0m, amount - regularLeft);
        var maximumGift = Math.Min(amount, giftLeft);
        var gift = Math.Clamp(giftTarget, minimumGift, maximumGift);

        return new PaymentShares(amount - gift, gift);
    }

    private static decimal CumulativeShare(decimal paid, int cancelledQuantity, int totalQuantity) =>
        decimal.Floor(paid * cancelledQuantity / totalQuantity);
}
