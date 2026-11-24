using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

/// <summary>
/// لغو کامل قلم دارای مرسولهٔ پرداخت‌نشده: برگشت نور بدون تغییر، و هشدار پاک‌سازی دستی مرسوله.
/// </summary>
public sealed class PhysicalCancellationCarrierWarningTests
{
    [Fact]
    public void FullCancellationWithPendingParcelRefundsEverythingAndWarnsAdmin()
    {
        var purchase = CreatePendingPaymentPurchase();
        var cart = CreateCart(purchase);

        var result = PhysicalCancellationPlanner.Plan(
            cart, [purchase], [new PhysicalCancellationTarget(purchase, 1)], new PaymentShares(0m, 0m));

        result.IsSuccess.Should().BeTrue();
        var line = result.Value.Lines.Single();
        line.Quantity.Should().Be(1);
        line.Refund.RegularInNoor.Should().Be(80m);
        line.Refund.GiftInNoor.Should().Be(20m);
        result.Value.ShippingRefund.TotalInNoor.Should().Be(0m);
        result.Value.WarningCodes.Should().Contain(PhysicalCancellationWarningCodes.CarrierPaymentPending);

        // مرسولهٔ پرداخت‌نشده پس از لغو هم روی قلم می‌ماند تا پاک‌سازی دستی شود.
        purchase.RecordCancellation(1, line.Refund);
        purchase.ProviderParcelId.Should().Be("99");
        purchase.HasPendingCarrierPayment.Should().BeTrue();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.RefundedRegularInNoor.Should().Be(80m);
    }

    [Fact]
    public void CancellationWithoutPendingParcelHasNoCarrierWarning()
    {
        var purchase = CreatePurchase(pendingPayment: false);
        var cart = CreateCart(purchase);

        var result = PhysicalCancellationPlanner.Plan(
            cart, [purchase], [new PhysicalCancellationTarget(purchase, 1)], new PaymentShares(0m, 0m));

        result.IsSuccess.Should().BeTrue();
        result.Value.WarningCodes.Should().NotContain(PhysicalCancellationWarningCodes.CarrierPaymentPending);
    }

    [Fact]
    public void ShippedParcelStillCannotBeCancelled()
    {
        var purchase = CreatePurchase(pendingPayment: false);
        purchase.MarkShipped("IR-100", PhysicalProductPurchase.PostexProvider, "99");
        var cart = CreateCart(purchase);

        var result = PhysicalCancellationPlanner.Plan(
            cart, [purchase], [new PhysicalCancellationTarget(purchase, 1)], new PaymentShares(0m, 0m));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(PhysicalCancellationErrorCodes.AlreadyShipped);
    }

    private static Cart CreateCart(PhysicalProductPurchase purchase) =>
        Cart.Create(Guid.NewGuid(), purchase.BuyerId.Value, "buyer-ext", ["product-1"], couponCode: null, FinancialScope.TarheElahi);

    private static PhysicalProductPurchase CreatePendingPaymentPurchase() =>
        CreatePurchase(pendingPayment: true);

    private static PhysicalProductPurchase CreatePurchase(bool pendingPayment)
    {
        var purchase = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()), "product-1", "Book", 1, 100m, 100m, FinancialScope.TarheElahi);
        purchase.RecordPaymentShares(new PaymentShares(80m, 20m));
        purchase.RememberCarrierParcel(PhysicalProductPurchase.PostexProvider, "99", 1);
        if (pendingPayment)
        {
            purchase.MarkCarrierPaymentPending(DateTime.UtcNow);
        }

        return purchase;
    }
}
