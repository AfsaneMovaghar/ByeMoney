using ByeMoney.Application.Modules.Purchases.Carts.Services;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class PaymentShareAllocatorTests
{
    [Theory]
    [InlineData(0, new[] { 50, 50 })]
    [InlineData(100, new[] { 60, 40 })]
    [InlineData(1, new[] { 33, 33, 34 })]
    [InlineData(2, new[] { 1, 100 })]
    [InlineData(40, new[] { 0, 10, 90 })]
    [InlineData(99, new[] { 33, 33, 33, 1 })]
    public void SplitGift_SumsExactlyAndNeverExceedsPartAmount(int gift, int[] parts)
    {
        var amounts = parts.Select(x => (decimal)x).ToArray();

        var shares = PaymentShareAllocator.SplitGift(gift, amounts);

        shares.Sum().Should().Be(gift);
        for (var i = 0; i < amounts.Length; i++)
        {
            shares[i].Should().BeInRange(0m, amounts[i]);
        }
    }

    [Fact]
    public void SplitGift_GivesProportionalFloorShareAndRemainderToLastParts()
    {
        var shares = PaymentShareAllocator.SplitGift(10, [105m, 105m, 25m]);

        // 10*105/235 = 4.46 و 10*25/235 = 1.06؛ باقی‌ماندهٔ ۱ از آخر به عقب پخش می‌شود
        shares.Should().Equal(4m, 4m, 2m);
    }
}
