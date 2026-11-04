using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class PhysicalRefundCalculatorTests
{
    [Theory]
    [InlineData(3, 100, 0, new[] { 33, 33, 34 })]
    [InlineData(3, 100, 40, new[] { 33, 33, 34 })]
    [InlineData(5, 3, 2, new[] { 0, 1, 0, 1, 1 })]
    [InlineData(4, 7, 7, new[] { 1, 2, 2, 2 })]
    public void UnitByUnitRefunds_GiveRemainderToLastUnit_AndNeverExceedEachShare(
        int quantity, int paid, int gift, int[] expectedTotals)
    {
        var purchase = Purchase(quantity, paid, gift);
        var totals = new List<decimal>();

        for (var unit = 0; unit < quantity; unit++)
        {
            var refund = PhysicalRefundCalculator.RefundFor(purchase, 1);
            refund.RegularInNoor.Should().BeGreaterThanOrEqualTo(0m);
            refund.GiftInNoor.Should().BeGreaterThanOrEqualTo(0m);
            purchase.RecordCancellation(1, refund);
            totals.Add(refund.TotalInNoor);
        }

        totals.Should().Equal(expectedTotals.Select(x => (decimal)x));
        purchase.RefundedRegularInNoor.Should().Be(paid - gift);
        purchase.RefundedGiftInNoor.Should().Be(gift);
    }

    [Fact]
    public void MultiUnitCancellation_MatchesSumOfSingleUnitCancellations()
    {
        var purchase = Purchase(3, 100, 40);

        var firstTwo = PhysicalRefundCalculator.RefundFor(purchase, 2);
        purchase.RecordCancellation(2, firstTwo);
        var last = PhysicalRefundCalculator.RefundFor(purchase, 1);

        firstTwo.TotalInNoor.Should().Be(66m);
        last.TotalInNoor.Should().Be(34m);
        (firstTwo.GiftInNoor + last.GiftInNoor).Should().Be(40m);
    }

    private static PhysicalProductPurchase Purchase(int quantity, decimal paid, decimal gift)
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), UserId.New(), "book", "کتاب", quantity, paid, paid);
        purchase.RecordPaymentShares(new PaymentShares(paid - gift, gift));
        return purchase;
    }
}
