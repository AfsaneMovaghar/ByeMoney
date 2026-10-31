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
        var account = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(account.Id, buyer);
        wallet.ApplyCredit(1000);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["chapter-uuid"], null);
        var json = $$"""
            {"cartId":"{{cart.Id}}","buyerExternalUserId":"buyer","couponCode":null,
             "items":[{"externalCourseId":"chapter-uuid","type":"course_chapter",
             "parentExternalId":"parent-document","title":"ÙØµÙ„","basePriceInNoor":120,
             "itemDiscountInNoor":0,"couponDiscountInNoor":20,"priceInNoor":100}]}
            """;
        using var http = new HttpClient(new QuoteResponseHandler(json)) { BaseAddress = new Uri("https://strapi.test/") };
        var client = new CartPricingClient(http);
        var quote = await client.PrepareAsync(new CartQuoteRequest(cart.Id, "buyer", ["chapter-uuid"], null), default);
        quote.IsSuccess.Should().BeTrue();
        cart.SetQuote(quote.Value.Items);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount());

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
        var account = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(account.Id, buyer);
        wallet.ApplyCredit(1000);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["a", "b"], "OFF");
        var firstPayment = 105 - firstDiscount - firstCoupon;
        var secondPayment = 105 - secondDiscount - secondCoupon;
        cart.SetQuote([
            new("a", "Ø§Ù„Ù", 105, firstDiscount, firstCoupon, firstPayment),
            new("b", "Ø¨", 105, secondDiscount, secondCoupon, secondPayment)
        ]);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet),
            cart.Total > 0 ? Account.CreateSystemAccount() : null, referenceRate: 137m);

        cart.Items.Select(x => x.PriceInNoor).Should().Equal(105m, 105m);
        payment.Purchases.Select(x => x.Snapshot.PriceInNoorAtPurchaseTime)
            .Should().Equal(firstPayment, secondPayment);
        payment.Purchases.Should().OnlyContain(x => x.Snapshot.ReferenceRialPerNoorAtPurchaseTime == 137m);
        wallet.WithdrawableBalance.Should().Be(1000 - firstPayment - secondPayment);
        payment.LedgerEntries.Sum(x => x.Amount).Should().Be(0);
        payment.LedgerEntries.Where(x => x.Amount < 0).Sum(x => -x.Amount).Should().Be(cart.Total);
        payment.LedgerEntries.Should().NotContain(x => x.Amount == 0);
        cart.Status.Should().Be(CartStatus.Paid);

        Action replay = () => CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount());
        replay.Should().Throw<InvalidOperationException>();
        wallet.WithdrawableBalance.Should().Be(1000 - firstPayment - secondPayment);
    }

    [Fact]
    public void FreeCart_TransitionsPurchasesToDebited_CreatesNoLedger_AndAllowsNotificationSent()
    {
        // Arrange
        var buyer = new UserId(Guid.NewGuid());
        var account = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(account.Id, buyer);
        wallet.ApplyCredit(500m);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-free", ["course-free-1", "course-free-2"], "100OFF");

        cart.SetQuote([
            new("course-free-1", "Ø¯ÙˆØ±Ù‡ Ø§ÙˆÙ„", 200m, 0m, 200m, 0m),
            new("course-free-2", "Ø¯ÙˆØ±Ù‡ Ø¯ÙˆÙ…", 150m, 50m, 100m, 0m)
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
}
