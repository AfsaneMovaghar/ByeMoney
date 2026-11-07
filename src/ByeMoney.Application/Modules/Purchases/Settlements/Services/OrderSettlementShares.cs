using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Services;

/// <summary>
/// سهم عادی و هدیهٔ پرداخت‌های لازم برای تفکیک نور خالص سفارش‌ها.
/// <c>ItemShares</c> بر اساس شناسهٔ خرید، <c>ShippingByCart</c> سهم ارسال و <c>RefundsByCart</c>
/// جمع برگشتی لغو (اقلام و ارسال) هر سبد است.
/// </summary>
public sealed record OrderSettlementPayments(
    IReadOnlyDictionary<Guid, PaymentShares> ItemShares,
    IReadOnlyDictionary<Guid, PaymentShares> ShippingByCart,
    IReadOnlyDictionary<Guid, PaymentShares> RefundsByCart);

/// <summary>
/// تفکیک نور خالص سفارش به سهم عادی و هدیه (D10): سهم پرداخت اقلام (D08) به‌علاوهٔ سهم ارسال سبد،
/// منهای سهم برگشتی لغو. اگر سهم پرداخت قلمی ثبت نشده باشد، تفکیک ممکن نیست و <c>null</c> برمی‌گردد.
/// </summary>
public static class OrderSettlementShares
{
    public static OrderSettlementPayments BuildPayments(
        CourseOrderSnapshot snapshot,
        IReadOnlyDictionary<Guid, PaymentShares> shippingByCart)
    {
        var itemShares = new Dictionary<Guid, PaymentShares>();

        foreach (var purchase in snapshot.Purchases)
        {
            if (purchase.RegularAmountInNoor.HasValue && purchase.GiftAmountInNoor.HasValue)
            {
                itemShares.TryAdd(
                    purchase.Id.Value,
                    new PaymentShares(purchase.RegularAmountInNoor.Value, purchase.GiftAmountInNoor.Value));
            }
        }

        foreach (var purchase in snapshot.PhysicalPurchases.Where(purchase => purchase.HasPaymentShares))
        {
            itemShares.TryAdd(
                purchase.Id.Value,
                new PaymentShares(purchase.RegularAmountInNoor!.Value, purchase.GiftAmountInNoor!.Value));
        }

        var refundsByCart = snapshot.PhysicalCancellations
            .GroupBy(cancellation => cancellation.CartId)
            .ToDictionary(
                group => group.Key,
                group => new PaymentShares(
                    group.Sum(cancellation => cancellation.RegularRefundInNoor + cancellation.ShippingRegularRefundInNoor),
                    group.Sum(cancellation => cancellation.GiftRefundInNoor + cancellation.ShippingGiftRefundInNoor)));

        return new OrderSettlementPayments(itemShares, shippingByCart, refundsByCart);
    }

    public static PaymentShares? Calculate(CourseOrderDetails order, OrderSettlementPayments payments)
    {
        var regular = 0m;
        var gift = 0m;

        foreach (var item in order.Items)
        {
            if (item.PurchaseId is not { } purchaseId ||
                !payments.ItemShares.TryGetValue(purchaseId, out var shares))
            {
                return null;
            }

            regular += shares.RegularInNoor;
            gift += shares.GiftInNoor;
        }

        if (order.CartId is { } cartId)
        {
            if (payments.ShippingByCart.TryGetValue(cartId, out var shipping))
            {
                regular += shipping.RegularInNoor;
                gift += shipping.GiftInNoor;
            }

            if (payments.RefundsByCart.TryGetValue(cartId, out var refund))
            {
                regular -= refund.RegularInNoor;
                gift -= refund.GiftInNoor;
            }
        }

        if (regular < 0 || gift < 0)
        {
            return null;
        }

        return new PaymentShares(regular, gift);
    }
}
