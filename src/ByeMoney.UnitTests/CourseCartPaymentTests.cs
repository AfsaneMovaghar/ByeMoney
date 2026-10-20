using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class CourseCartPaymentTests
{
    [Theory]
    [InlineData(0, 21, 0, 0)]
    [InlineData(10, 20, 0, 0)]
    [InlineData(0, 0, 105, 0)]
    [InlineData(0, 105, 105, 0)]
    public void PaymentUsesQuotedAllocationAndKeepsItemPricesFixed(
        decimal firstDiscount,
        decimal firstCoupon,
        decimal secondDiscount,
        decimal secondCoupon)
    {
        var buyer = new UserId(Guid.NewGuid());
        var account = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(account.Id, buyer);
        wallet.ApplyCredit(1000);
        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["a", "b"], "OFF");
        var firstPayment = 105 - firstDiscount - firstCoupon;
        var secondPayment = 105 - secondDiscount - secondCoupon;
        cart.SetQuote([
            new("a", "الف", 105, firstDiscount, firstCoupon, firstPayment),
            new("b", "ب", 105, secondDiscount, secondCoupon, secondPayment)
        ]);

        var payment = CourseCartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet),
            cart.Total > 0 ? Account.CreateSystemAccount() : null, referenceRate: 137m);

        cart.Items.Select(x => x.PriceInNoor).Should().Equal(105m, 105m);
        payment.Purchases.Select(x => x.Snapshot.PriceInNoorAtPurchaseTime)
            .Should().Equal(firstPayment, secondPayment);
        payment.Purchases.Should().OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 137m);
        wallet.Balance.Should().Be(1000 - firstPayment - secondPayment);
        payment.LedgerEntries.Sum(x => x.Amount).Should().Be(0);
        payment.LedgerEntries.Where(x => x.Amount < 0).Sum(x => -x.Amount).Should().Be(cart.Total);
        payment.LedgerEntries.Should().NotContain(x => x.Amount == 0);
        cart.Status.Should().Be(CourseCartStatus.Paid);

        Action replay = () => CourseCartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount());
        replay.Should().Throw<InvalidOperationException>();
        wallet.Balance.Should().Be(1000 - firstPayment - secondPayment);
    }
}
