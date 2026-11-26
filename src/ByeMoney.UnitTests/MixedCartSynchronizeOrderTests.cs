using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class MixedCartSynchronizeOrderTests
{
    [Fact]
    public async Task SynchronizeAsync_OrdersPurchaseIdsByCanonicalWireTypeThenOrdinalExternalId_WhenProductIdIsSmallerThanDigitalIds()
    {
        var buyerId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cart = Cart.Create(
            cartId,
            buyerId,
            "buyer-ext",
            [
                new CartItemInput("a-product", ProductItemType.Product, 2),
                new CartItemInput("b-chapter", ProductItemType.CourseChapter, 1),
                new CartItemInput("c-course", ProductItemType.Course, 1)
            ],
            couponCode: null,
            FinancialScope.TarheElahi);

        cart.SetQuote(
            [
                new CartQuoteItem("a-product", "کالا", 50m, 0m, 0m, 100m, ProductItemType.Product, Quantity: 2),
                new CartQuoteItem("b-chapter", "فصل", 80m, 0m, 0m, 80m, ProductItemType.CourseChapter, ParentExternalId: "c-course", Quantity: 1),
                new CartQuoteItem("c-course", "دوره", 200m, 0m, 0m, 200m, ProductItemType.Course, Quantity: 1)
            ],
            shippingAmountInNoor: 10m,
            shippingRuleLabel: "ارسال ثابت داخلی",
            shippingAddress: new ShippingAddress("تهران", "تهران", "city-1", "1234567890", "خیابان آزادی", "علی", "09120000000"),
            shippingOriginId: "ramsar",
            shippingOriginLabel: "رامسر",
            shippingOriginCityId: 10);

        // ترتیب Cart.Items و تخصیص مالی همچنان بر اساس ExternalItemId باقی می‌ماند.
        cart.Items.Select(x => x.ExternalItemId).Should().Equal("a-product", "b-chapter", "c-course");

        var account = Account.CreateUserAccount(new UserId(buyerId), FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, new UserId(buyerId), FinancialScope.TarheElahi);
        wallet.ApplyCredit(1000m);
        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount(FinancialScope.TarheElahi));
        cart.Status.Should().Be(CartStatus.Paid);
        payment.Purchases.Select(x => x.Id).Should().Equal(
            CartPurchaseIds.For(cartId, "b-chapter"),
            CartPurchaseIds.For(cartId, "c-course"));
        payment.PhysicalPurchases.Select(x => x.Id.Value).Should().Equal(
            CartPurchaseIds.For(cartId, "a-product").Value);

        CartTransitionRequest? capturedCommit = null;
        var store = new Mock<ICartStore>();
        store.Setup(x => x.FindAsync(cartId, buyerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var pricing = new Mock<ICartPricingClient>();
        pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>()))
            .Callback<CartTransitionRequest, string, CancellationToken>((req, _, _) => capturedCommit = req)
            .ReturnsAsync(Result.Success());

        var service = new CartService(
            store.Object,
            pricing.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IWalletRepository>(),
            Mock.Of<ISystemSettingRepository>(),
            Mock.Of<ICoursePurchaseRepository>(),
            Mock.Of<ITopUpRequestRepository>());

        await service.SynchronizeAsync(cartId, buyerId, CancellationToken.None);

        capturedCommit.Should().NotBeNull();
        capturedCommit!.PurchaseIds.Should().Equal(
            CartPurchaseIds.For(cartId, "c-course").Value,
            CartPurchaseIds.For(cartId, "b-chapter").Value,
            CartPurchaseIds.For(cartId, "a-product").Value);

        store.Verify(
            x => x.RecordSynchronizationAsync(cartId, buyerId, true, null, cart.ReservationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
