using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Interfaces;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class CourseCartRefreshTests
{
    private readonly Mock<ICourseCartStore> _store = new();
    private readonly Mock<ICartPricingClient> _pricing = new();
    private readonly User _buyer = User.CreateInitial("buyer");
    private CourseCartService Service()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(_buyer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_buyer);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(137);
        return new(_store.Object, _pricing.Object, users.Object, Mock.Of<IWalletRepository>(), settings.Object,
            Mock.Of<ICoursePurchaseRepository>(), Mock.Of<ITopUpRequestRepository>());
    }
    private CourseCart Ready()
    {
        var cart = CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, "buyer", ["course"], null);
        cart.SetQuote([new("course", "دوره", 105, 0, 0, 105)]);
        _store.Setup(x => x.CreateAsync(It.IsAny<CourseCart>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseCart>.Success(cart));
        _store.Setup(x => x.FindAsync(cart.Id, _buyer.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        return cart;
    }
    [Fact]
    public async Task ReopeningReadyCartRejectsDeletedCourseBeforeAnyPayment()
    {
        var cart = Ready();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));
        var result = await Service().PrepareAsync(_buyer.Id.Value, cart.Id, ["course"], null, default);
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        _pricing.Verify(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(x => x.PayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Never);
    }
    [Fact]
    public async Task ReopeningAvailableCartPreservesFrozenPrice()
    {
        var cart = Ready();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var result = await Service().PrepareAsync(_buyer.Id.Value, cart.Id, ["course"], null, default);
        result.Value.PriceInNoor.Should().Be(105);
        result.Value.Courses.Single().PriceInNoor.Should().Be(105);
        _pricing.Verify(x => x.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task CatalogOutageDoesNotInvalidateReservationOrPermitPayment()
    {
        var cart = Ready();
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        var result = await Service().PrepareAsync(_buyer.Id.Value, cart.Id, ["course"], null, default);
        result.ErrorCode.Should().Be(CartErrorCodes.Unavailable);
        cart.Status.Should().Be(CourseCartStatus.Ready);
        _store.Verify(x => x.FailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task CartAwaitingBankResultIsReturnedWithoutRepricing()
    {
        var cart = Ready(); cart.AwaitTopUp(Guid.NewGuid());
        var result = await Service().PrepareAsync(_buyer.Id.Value, cart.Id, ["course"], null, default);
        result.Value.Status.Should().Be("AwaitingTopUp");
        result.Value.PriceInNoor.Should().Be(105);
        _pricing.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task CartResponseKeepsCoursePricesFixedAndDiscountsOnlyOnCart()
    {
        var cart = CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, "buyer", ["a", "b"], "OFF");
        cart.SetQuote([new("a", "الف", 105, 5, 11, 89), new("b", "ب", 105, 0, 10, 95)]);
        _store.Setup(x => x.FindAsync(cart.Id, _buyer.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var response = (await Service().GetAsync(_buyer.Id.Value, cart.Id, default)).Value;

        response.Courses.Select(x => x.PriceInNoor).Should().Equal(105m, 105m);
        response.OriginalTotalInNoor.Should().Be(210);
        response.ItemDiscountInNoor.Should().Be(5);
        response.CouponDiscountInNoor.Should().Be(21);
        response.PriceInNoor.Should().Be(184);
        response.ShortfallInNoor.Should().Be(184);

        var json = System.Text.Json.JsonSerializer.SerializeToElement(response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        foreach (var course in json.GetProperty("courses").EnumerateArray())
        {
            course.TryGetProperty("basePriceInNoor", out _).Should().BeFalse();
            course.TryGetProperty("itemDiscountInNoor", out _).Should().BeFalse();
            course.TryGetProperty("couponDiscountInNoor", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task FailedReleasePreventsClaimingThatCancellationCompleted()
    {
        var cart = Ready(); cart.Fail();
        _store.Setup(x => x.CancelAsync(cart.Id, _buyer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseCart>.Success(cart));
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        (await Service().CancelAsync(_buyer.Id.Value, cart.Id, default)).ErrorCode.Should().Be(CartErrorCodes.Unavailable);
    }

    [Fact]
    public async Task CartAwaitingTopUp_WithUnknownResultAndReviewCase_ExposesUnderReviewStatus()
    {
        var topUp = TopUpRequest.CreateGateway(new UserId(_buyer.Id.Value), 105m, 137m);
        topUp.RecordGatewayResult("evt-1", "SEP", GatewayResultKinds.Unknown, null, null, "-6", 14385m, 14385m, DateTime.UtcNow);
        topUp.OpenFinancialReview("case-cart-1", "VERIFY_UNKNOWN");

        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);

        var cart = CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, "buyer", ["course"], null);
        cart.SetQuote([new("course", "دوره", 105, 0, 0, 105)]);
        cart.AwaitTopUp(topUp.Id.Value);

        _store.Setup(x => x.FindAsync(cart.Id, _buyer.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(_buyer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_buyer);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(137);

        var service = new CourseCartService(_store.Object, _pricing.Object, users.Object,
            Mock.Of<IWalletRepository>(), settings.Object, Mock.Of<ICoursePurchaseRepository>(), topUps.Object);

        var response = (await service.GetAsync(_buyer.Id.Value, cart.Id, default)).Value;

        response.Status.Should().Be("AwaitingTopUp");
        response.TopUpStatus.Should().Be("Pending");
        response.IsUnderReview.Should().BeTrue();
        response.ReviewCaseId.Should().Be("case-cart-1");
        response.ReviewReasonCode.Should().Be("VERIFY_UNKNOWN");
        response.GatewayResultKind.Should().Be(GatewayResultKinds.Unknown);
    }
}
