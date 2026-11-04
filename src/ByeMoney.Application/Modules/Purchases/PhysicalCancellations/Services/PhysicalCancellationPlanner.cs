using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

public sealed record PhysicalCancellationTarget(PhysicalProductPurchase Purchase, int Quantity);

public sealed record PhysicalCancellationLine(PhysicalProductPurchase Purchase, int Quantity, PaymentShares Refund);

public sealed record PhysicalCancellationPlan(
    IReadOnlyList<PhysicalCancellationLine> Lines,
    PaymentShares ShippingRefund,
    IReadOnlyList<string> WarningCodes);

/// <summary>
/// قواعد D09 برای لغو کالای فیزیکی، بدون اثر جانبی. قیمت، کوپن و ارسال رایگان پس از لغو دوباره محاسبه نمی‌شوند.
/// </summary>
public static class PhysicalCancellationPlanner
{
    public static Result<PhysicalCancellationPlan> Plan(
        Cart cart,
        IReadOnlyList<PhysicalProductPurchase> cartPurchases,
        IReadOnlyList<PhysicalCancellationTarget> targets,
        PaymentShares shippingPaid)
    {
        if (targets.Count == 0)
        {
            return Conflict();
        }

        if (targets.Any(x => x.Purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing))
        {
            return Result<PhysicalCancellationPlan>.Conflict(
                PhysicalCancellationErrors.AlreadyShipped, PhysicalCancellationErrorCodes.AlreadyShipped);
        }

        if (targets.Any(x => !x.Purchase.HasPaymentShares))
        {
            return ManualReview();
        }

        if (targets.Any(x => x.Quantity < 1 || x.Quantity > x.Purchase.RemainingQuantity))
        {
            return Conflict();
        }

        var lines = targets
            .Select(x => new PhysicalCancellationLine(
                x.Purchase, x.Quantity, PhysicalRefundCalculator.RefundFor(x.Purchase, x.Quantity)))
            .ToArray();

        // ارسال فقط وقتی برمی‌گردد که این لغو، کل مرسوله را پیش از ارسال تمام کند.
        var completesShipment = cartPurchases.All(purchase =>
            purchase.RemainingQuantity == targets.Where(x => x.Purchase.Id == purchase.Id).Sum(x => x.Quantity));

        var shippingRefund = new PaymentShares(0m, 0m);
        if (completesShipment && cart.ShippingAmountInNoor > 0)
        {
            if (shippingPaid.TotalInNoor != cart.ShippingAmountInNoor)
            {
                return ManualReview();
            }

            shippingRefund = shippingPaid;
        }

        return Result<PhysicalCancellationPlan>.Success(
            new PhysicalCancellationPlan(lines, shippingRefund, Warnings(cart, completesShipment)));
    }

    private static IReadOnlyList<string> Warnings(Cart cart, bool completesShipment)
    {
        var warnings = new List<string>();

        if (cart.CouponCode is not null)
        {
            warnings.Add(PhysicalCancellationWarningCodes.CouponRemoved);
        }

        if (cart.ShippingAmountInNoor == 0)
        {
            warnings.Add(PhysicalCancellationWarningCodes.FreeShippingRemoved);
        }
        else if (!completesShipment)
        {
            warnings.Add(PhysicalCancellationWarningCodes.ShippingNotRefunded);
        }

        return warnings;
    }

    private static Result<PhysicalCancellationPlan> Conflict() =>
        Result<PhysicalCancellationPlan>.Conflict(
            PhysicalCancellationErrors.Conflict, PhysicalCancellationErrorCodes.Conflict);

    private static Result<PhysicalCancellationPlan> ManualReview() =>
        Result<PhysicalCancellationPlan>.Conflict(
            PhysicalCancellationErrors.ManualReview, PhysicalCancellationErrorCodes.ManualReview);
}
