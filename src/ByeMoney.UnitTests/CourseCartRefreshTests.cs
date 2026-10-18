using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Cart;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
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
        _store.Verify(x => x.PayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task FailedReleasePreventsClaimingThatCancellationCompleted()
    {
        var cart = Ready(); cart.Fail();
        _store.Setup(x => x.CancelAsync(cart.Id, _buyer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseCart>.Success(cart));
        _pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable));
        (await Service().CancelAsync(_buyer.Id.Value, cart.Id, default)).ErrorCode.Should().Be(CartErrorCodes.Unavailable);
    }
}
