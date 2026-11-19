using System.Text.Json;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class CartOriginCityGuardTests
{
    [Fact]
    public async Task MismatchedOriginCityConflictsBeforeFreezeOrPayment()
    {
        var harness = new Harness(quotedCityId: 259, configuredCityId: 573);

        var result = await harness.PrepareAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        result.ErrorMessage.Should().Be(CartErrors.OriginCityMismatch);
        harness.Store.Verify(s => s.SetQuoteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<CartQuoteItem>>(),
            It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<ShippingAddress?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Store.Verify(s => s.PayAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Fact]
    public async Task MatchingOriginCityFreezesWithoutExposingTheCity()
    {
        var harness = new Harness(quotedCityId: 573, configuredCityId: 573);

        var result = await harness.PrepareAsync();

        result.IsSuccess.Should().BeTrue();
        harness.Live.ShippingOriginId.Should().Be("ramsar");
        harness.Live.ShippingOriginCityId.Should().Be(573);
        harness.Live.Status.Should().Be(CartStatus.Ready);
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().NotContain("shippingOriginCityId");
        harness.Store.Verify(s => s.PayAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Fact]
    public async Task MissingOriginCityDoesNotBlockCheckout()
    {
        var harness = new Harness(quotedCityId: null, configuredCityId: 111);

        var result = await harness.PrepareAsync();

        result.IsSuccess.Should().BeTrue();
        harness.Live.ShippingOriginCityId.Should().BeNull();
        harness.Live.Status.Should().Be(CartStatus.Ready);
    }

    private sealed class Harness
    {
        public Harness(int? quotedCityId, int? configuredCityId)
        {
            Buyer = User.CreateInitial("buyer");
            Cart = Cart.Create(
                Guid.NewGuid(),
                Buyer.Id.Value,
                Buyer.ExternalUserId,
                [new CartItemInput("prod-1", ProductItemType.Product, 1)],
                null);
            Store = new Mock<ICartStore>();
            var pricing = new Mock<ICartPricingClient>();
            var users = new Mock<IUserRepository>();
            var settings = new Mock<ISystemSettingRepository>();
            users.Setup(x => x.GetByIdAsync(Buyer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Buyer);
            settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
            Store.Setup(x => x.CreateAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Cart cart, CancellationToken _) =>
                {
                    Live = cart;
                    return Result<Cart>.Success(cart);
                });
            Store.Setup(x => x.SetQuoteAsync(
                    Cart.Id, Buyer.Id.Value, It.IsAny<IReadOnlyList<CartQuoteItem>>(),
                    It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<ShippingAddress?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(),
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, IReadOnlyList<CartQuoteItem> items, decimal shipping, string? rule,
                    ShippingAddress? address, string? originId, string? originLabel, int? originCityId, Guid _,
                    CancellationToken _) =>
                {
                    Live.SetQuote(items, shipping, rule, address, originId, originLabel, originCityId);
                    return Live;
                });
            pricing.Setup(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CartQuoteRequest request, CancellationToken _) => Result<CartQuote>.Success(new CartQuote(
                    request.CartId,
                    request.BuyerExternalUserId,
                    request.CouponCode,
                    [new CartQuoteItem("prod-1", "کالا", 100, 0, 0, 100, ProductItemType.Product)],
                    PriceInNoor: 100,
                    ShippingOriginId: "ramsar",
                    ShippingOriginLabel: "رامسر",
                    ShippingOriginCityId: quotedCityId)));
            pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());

            Service = new CartService(
                Store.Object,
                pricing.Object,
                users.Object,
                Mock.Of<IWalletRepository>(),
                settings.Object,
                Mock.Of<ICoursePurchaseRepository>(),
                Mock.Of<ITopUpRequestRepository>(),
                new FixedOriginCity(configuredCityId));
        }

        public User Buyer { get; }

        public Cart Cart { get; }

        public Cart Live { get; private set; } = null!;

        public Mock<ICartStore> Store { get; }

        public CartService Service { get; }

        public Task<Result<CartResponse>> PrepareAsync() =>
            Service.PrepareAsync(
                Buyer.Id.Value,
                Cart.Id,
                [new CartItemInput("prod-1", ProductItemType.Product, 1)],
                null,
                CancellationToken.None);
    }

    private sealed class FixedOriginCity(int? cityId) : IShippingOriginCityReader
    {
        public int? GetCityId(string? shippingOriginId) => cityId;
    }
}
