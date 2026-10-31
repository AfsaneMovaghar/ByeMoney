using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class CartTests
{
    private static Cart Cart() => Cart.Create(Guid.NewGuid(), Guid.NewGuid(), "user", ["a", "b"], "off");
    private static CartQuoteItem[] Items() => [new("a", "الف", 105, 0, 11, 94), new("b", "ب", 105, 0, 10, 95)];
    [Fact]
    public void FrozenCart_PreservesAggregateCouponAndCanonicalIdentity()
    {
        var cart = Cart(); cart.SetQuote(Items());
        cart.Total.Should().Be(189);
        cart.Items.Select(x => x.PriceInNoor).Should().Equal(105m, 105m);
        cart.ItemDiscountInNoor.Should().Be(0);
        cart.CouponDiscountInNoor.Should().Be(21);
        cart.Items.Select(x => x.AllocatedAmountInNoor).Should().Equal(94m, 95m);
        cart.CouponCode.Should().Be("OFF");
        cart.Matches(["b", "a"], " off ").Should().BeTrue();
        cart.Matches(["a", "b"], "other").Should().BeFalse();
        cart.Matches(["a"], "OFF").Should().BeFalse();
        cart.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }
    [Theory]
    [InlineData(-1, 0, 106)]
    [InlineData(0, 106, -1)]
    [InlineData(0, 10, 94)]
    [InlineData(0, 10.5, 94.5)]
    public void InvalidFinancialSnapshotIsRejected(decimal itemDiscount, decimal couponDiscount, decimal final)
    {
        var cart = Cart();
        Action act = () => cart.SetQuote([new("a", "الف", 105, itemDiscount, couponDiscount, final), Items()[1]]);
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void SnapshotCannotChangeAfterPreparationOrPayment()
    {
        var cart = Cart(); cart.SetQuote(Items()); cart.MarkPaid();
        cart.SetQuote([new("a", "تغییر", 1, 0, 0, 1)]);
        cart.Fail(); cart.Fail(expired: true);
        cart.Status.Should().Be(CartStatus.Paid);
        cart.Total.Should().Be(189);
    }
    [Fact]
    public void FreeRowsAndEntirelyFreeCartAreValid()
    {
        var cart = Cart(); cart.SetQuote([new("a", "الف", 105, 0, 105, 0), new("b", "ب", 105, 105, 0, 0)]);
        cart.Total.Should().Be(0); cart.MarkPaid(); cart.Status.Should().Be(CartStatus.Paid);
    }
    [Fact]
    public void ExpirationCannotCancelTopUpOrFreshCart()
    {
        var cart = Cart(); cart.SetQuote(Items()); cart.Fail(expired: true);
        cart.Status.Should().Be(CartStatus.Ready);
        cart.AwaitTopUp(Guid.NewGuid()); cart.Fail(expired: true);
        cart.Status.Should().Be(CartStatus.AwaitingTopUp);
    }
    [Fact]
    public void PurchaseIdsAreStableAndDistinctAcrossCartAndCourse()
    {
        var cart = Guid.NewGuid();
        CartPurchaseIds.For(cart, "a").Should().Be(CartPurchaseIds.For(cart, "a"));
        CartPurchaseIds.For(cart, "a").Should().NotBe(CartPurchaseIds.For(cart, "b"));
        CartPurchaseIds.For(cart, "a").Should().NotBe(CartPurchaseIds.For(Guid.NewGuid(), "a"));
    }
    [Fact]
    public void RetryPreservesCartAndPurchaseIdentityAndKeepsTopUpHistory()
    {
        var cart = Cart(); cart.SetQuote(Items());
        var cartId = cart.Id;
        var reservation = cart.ReservationId;
        var topUp = Guid.NewGuid(); cart.AwaitTopUp(topUp);
        Action premature = () => cart.Reopen();
        premature.Should().Throw<InvalidOperationException>();
        cart.Fail();
        premature.Should().Throw<InvalidOperationException>();
        cart.RecordSynchronization(true, null); cart.Reopen();
        cart.Id.Should().Be(cartId);
        cart.ReservationId.Should().NotBe(reservation);
        cart.TopUpRequestIds.Should().Equal(topUp);
        cart.TopUpRequestId.Should().BeNull();
        cart.Status.Should().Be(CartStatus.Preparing);
        cart.SetQuote([new("a", "الف", 200, 0, 20, 180), new("b", "ب", 100, 0, 10, 90)]);
        cart.Total.Should().Be(270);
        cart.AwaitTopUp(Guid.NewGuid());
        cart.TopUpRequestIds.Should().HaveCount(2);
    }
    [Fact]
    public async Task ValidationRejectsInvalidListCouponAndIdWithoutInfrastructureCalls()
    {
        var validator = new PrepareCartValidator();
        (await validator.ValidateAsync(new PrepareCartCommand(Guid.NewGuid(), ["a", "a"], null))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new PrepareCartCommand(Guid.NewGuid(), ["a"], " "))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new PrepareCartCommand(Guid.Empty, ["a"], null))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new PrepareCartCommand(Guid.NewGuid(), [" a"], null))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new PrepareCartCommand(Guid.NewGuid(), ["a", "b"], "OFF"))).IsValid.Should().BeTrue();
    }
}

