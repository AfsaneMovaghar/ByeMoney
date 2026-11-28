using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ByeMoney.UnitTests;

public sealed class CartPaymentTests
{
    [Fact]
    public async Task ChapterQuoteFromStrapiPreservesFinancialAndDeliveryIdentity()
    {
        var buyer = UserId.New();
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.ApplyCredit(1000);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["chapter-uuid"], null, FinancialScope.TarheElahi);
        var json = $$"""
            {"cartId":"{{cart.Id}}","buyerExternalUserId":"buyer","couponCode":null,
             "items":[{"externalCourseId":"chapter-uuid","type":"course_chapter",
             "parentExternalId":"parent-document","title":"فصل","basePriceInNoor":120,
             "itemDiscountInNoor":0,"couponDiscountInNoor":20,"priceInNoor":100}]}
            """;
        using var http = new HttpClient(new QuoteResponseHandler(json)) { BaseAddress = new Uri("https://strapi.test/") };
        var client = new CartPricingClient(http);
        var quote = await client.PrepareAsync(new CartQuoteRequest(cart.Id, "buyer", null, [new CartItemInput("chapter-uuid", ProductItemType.CourseChapter)]), default);
        quote.IsSuccess.Should().BeTrue();
        cart.SetQuote(quote.Value.Items);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount(FinancialScope.TarheElahi));

        wallet.WithdrawableBalance.Should().Be(900);
        payment.LedgerEntries.Sum(entry => entry.Amount).Should().Be(0);
        var snapshot = payment.Purchases.Single().Snapshot;
        snapshot.ItemType.Should().Be(ProductItemType.CourseChapter);
        snapshot.ExternalProductId.Should().Be("chapter-uuid");
        snapshot.ExternalParentId.Should().Be("parent-document");
        using var item = JsonDocument.Parse(JsonSerializer.Serialize(PurchaseNotificationItem.FromSnapshot(snapshot),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        item.RootElement.GetProperty("type").GetString().Should().Be("course_chapter");
        item.RootElement.GetProperty("externalId").GetString().Should().Be("chapter-uuid");
        item.RootElement.GetProperty("parentExternalId").GetString().Should().Be("parent-document");
    }

    private sealed class QuoteResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

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
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.ApplyCredit(1000);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["a", "b"], "OFF", FinancialScope.TarheElahi);
        var firstPayment = 105 - firstDiscount - firstCoupon;
        var secondPayment = 105 - secondDiscount - secondCoupon;
        cart.SetQuote([
            new("a", "الف", 105, firstDiscount, firstCoupon, firstPayment),
            new("b", "ب", 105, secondDiscount, secondCoupon, secondPayment)
        ]);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet),
            cart.Total > 0 ? Account.CreateSystemAccount(FinancialScope.TarheElahi) : null, referenceRate: 137m);

        cart.Items.Select(x => x.PriceInNoor).Should().Equal(105m, 105m);
        payment.Purchases.Select(x => x.Snapshot.PriceInNoorAtPurchaseTime)
            .Should().Equal(firstPayment, secondPayment);
        payment.Purchases.Should().OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 137m);
        wallet.WithdrawableBalance.Should().Be(1000 - firstPayment - secondPayment);
        payment.LedgerEntries.Sum(x => x.Amount).Should().Be(0);
        payment.LedgerEntries.Where(x => x.Amount < 0).Sum(x => -x.Amount).Should().Be(cart.Total);
        payment.LedgerEntries.Should().NotContain(x => x.Amount == 0);
        cart.Status.Should().Be(CartStatus.Paid);

        Action replay = () => CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount(FinancialScope.TarheElahi));
        replay.Should().Throw<InvalidOperationException>();
        wallet.WithdrawableBalance.Should().Be(1000 - firstPayment - secondPayment);
    }

    [Fact]
    public void FreeCart_TransitionsPurchasesToDebited_CreatesNoLedger_AndAllowsNotificationSent()
    {
        // Arrange
        var buyer = new UserId(Guid.NewGuid());
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.ApplyCredit(500m);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-free", ["course-free-1", "course-free-2"], "100OFF", FinancialScope.TarheElahi);

        cart.SetQuote([
            new("course-free-1", "دوره اول", 200m, 0m, 200m, 0m),
            new("course-free-2", "دوره دوم", 150m, 50m, 100m, 0m)
        ], shippingAmountInNoor: 0m);

        cart.Total.Should().Be(0m);

        // Act
        var payment = CartPayment.Apply(
            cart,
            new ProvisionedUserWallet(account, wallet),
            system: null,
            referenceRate: 150m);

        // Assert
        cart.Status.Should().Be(CartStatus.Paid);
        wallet.WithdrawableBalance.Should().Be(500m);
        payment.LedgerEntries.Should().BeEmpty();
        payment.Purchases.Should().HaveCount(2);

        foreach (var purchase in payment.Purchases)
        {
            purchase.IsFree.Should().BeTrue();
            purchase.Status.Should().Be(CoursePurchaseStatus.Debited);
            purchase.LedgerTransactionId.Should().Be(cart.Id);

            // Access sync / notification transition must succeed without DomainException
            var act = () => purchase.MarkNotificationSent();
            act.Should().NotThrow();
            purchase.Status.Should().Be(CoursePurchaseStatus.NotificationSent);
        }
    }

    [Fact]
    public void MixedAndFreePhysicalCarts_RecordReferenceRateWithoutAffectingFinancials()
    {
        var buyer = new UserId(Guid.NewGuid());
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.ApplyCredit(1000m);

        var mixed = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-mixed", [
            new CartItemInput("course-1", ProductItemType.Course, 1),
            new CartItemInput("chapter-1", ProductItemType.CourseChapter, 1),
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ], null, FinancialScope.TarheElahi);
        mixed.SetQuote([
            new("course-1", "دوره", 100m, 0m, 0m, 100m, ProductItemType.Course, null, 1),
            new("chapter-1", "فصل", 50m, 0m, 0m, 50m, ProductItemType.CourseChapter, "course-2", 1),
            new("product-1", "کتاب", 40m, 0m, 0m, 80m, ProductItemType.Product, null, 2)
        ], shippingAmountInNoor: 20m);

        var payment = CartPayment.Apply(
            mixed,
            new ProvisionedUserWallet(account, wallet),
            Account.CreateSystemAccount(FinancialScope.TarheElahi),
            referenceRate: 321m);

        payment.Purchases.Should().HaveCount(2)
            .And.OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 321m);
        payment.PhysicalPurchases.Should().ContainSingle()
            .Which.ReferenceRialPerNoorAtPurchaseTime.Should().Be(321m);
        wallet.WithdrawableBalance.Should().Be(750m);
        payment.LedgerEntries.Where(x => x.Amount < 0).Sum(x => -x.Amount).Should().Be(250m);

        var freePhysical = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-free-phys", [
            new CartItemInput("product-free", ProductItemType.Product, 1)
        ], "FREE", FinancialScope.TarheElahi);
        freePhysical.SetQuote([
            new("product-free", "کتاب هدیه", 50m, 0m, 50m, 0m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 0m);

        var freePayment = CartPayment.Apply(
            freePhysical,
            new ProvisionedUserWallet(account, wallet),
            system: null,
            referenceRate: 654m);
        freePayment.PhysicalPurchases.Should().ContainSingle()
            .Which.ReferenceRialPerNoorAtPurchaseTime.Should().Be(654m);

        var nullRateCart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-null-rate", [
            new CartItemInput("product-null", ProductItemType.Product, 1)
        ], null, FinancialScope.TarheElahi);
        nullRateCart.SetQuote([
            new("product-null", "کتاب بدون نرخ", 30m, 0m, 0m, 30m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 0m);

        var nullPayment = CartPayment.Apply(
            nullRateCart,
            new ProvisionedUserWallet(account, wallet),
            Account.CreateSystemAccount(FinancialScope.TarheElahi),
            referenceRate: null);
        nullPayment.PhysicalPurchases.Should().ContainSingle()
            .Which.ReferenceRialPerNoorAtPurchaseTime.Should().BeNull();
        wallet.WithdrawableBalance.Should().Be(720m);
    }
}
