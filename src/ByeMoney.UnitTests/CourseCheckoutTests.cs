using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Queries.Checkout;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class CourseCheckoutTests
{
    private readonly UserId _userId = UserId.New();
    private readonly Mock<ICurrentUserService> _current = new();
    private readonly Mock<ICoursePurchaseRepository> _purchases = new();
    private readonly Mock<ITopUpRequestRepository> _topUps = new();
    private readonly Mock<IWalletRepository> _wallets = new();
    private readonly Mock<ITarhElahiIntegrationClient> _catalog = new();
    private readonly Mock<ISystemSettingRepository> _settings = new();

    public CourseCheckoutTests()
    {
        _current.SetupGet(x => x.UserId).Returns(_userId.Value);
        _catalog.Setup(x => x.GetCourseAsync("course-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto { ExternalId = "course-1", PriceNoor = 50m, Published = true, Available = true });
        _settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(137m);
    }

    private void SetBalance(decimal amount)
    {
        var wallet = Wallet.Create(Account.CreateUserAccount(_userId).Id, _userId);
        if (amount > 0) wallet.ApplyCredit(amount);
        _wallets.Setup(x => x.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
    }

    [Theory]
    [InlineData(0, 50, 6850)]
    [InlineData(20.2, 30, 4110)]
    [InlineData(50, 0, 0)]
    public async Task Preview_UsesServerPriceAndRoundsOnlyShortfallUp(double balance, int shortfall, int rial)
    {
        SetBalance((decimal)balance);
        var handler = new GetCourseCheckoutQueryHandler(_current.Object, _catalog.Object, _wallets.Object, _settings.Object, _purchases.Object);
        var response = await handler.Handle(new("course-1"), CancellationToken.None);
        response.PriceInNoor.Should().Be(50m);
        response.ShortfallInNoor.Should().Be(shortfall);
        response.AmountRial.Should().Be(rial);
    }

    [Fact]
    public async Task PurchaseTopUp_RecalculatesAmountAndSnapshotInsteadOfUsingBrowserValues()
    {
        SetBalance(20.2m);
        TopUpRequest? saved = null;
        _topUps.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TopUpRequest, CancellationToken>((topUp, _) => saved = topUp).Returns(Task.CompletedTask);
        var handler = new CreateTopUpRequestCommandHandler(_topUps.Object, Mock.Of<IUnitOfWork>(), _settings.Object, _catalog.Object, _wallets.Object);
        await handler.Handle(new(_userId.Value, 999m, PaymentMethod.Gateway,
            PendingItems: [new(PendingItemType.Course, "course-1", 1m)]), CancellationToken.None);
        saved!.AmountNoor.Should().Be(30m);
        saved.AmountRial.Should().Be(4110m);
        saved.PendingItems.Single().PriceNoorSnapshot.Should().Be(50m);
    }

    [Fact]
    public async Task TopUpStatus_HidesAnotherUsersRequest()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 50m, 137m);
        _topUps.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);
        var handler = new GetTopUpCheckoutQueryHandler(_current.Object, _topUps.Object, _purchases.Object);
        await FluentActions.Awaiting(() => handler.Handle(new(topUp.Id.Value), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
        _purchases.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, CheckoutStates.PurchaseIncomplete)]
    [InlineData(CoursePurchaseStatus.Debited, CheckoutStates.Activating)]
    [InlineData(CoursePurchaseStatus.NotificationFailed, CheckoutStates.DeliveryPending)]
    [InlineData(CoursePurchaseStatus.NotificationSent, CheckoutStates.Completed)]
    public async Task ConfirmedTopUp_DoesNotImplyDeliveredCourse(CoursePurchaseStatus? status, string expected)
    {
        var topUp = TopUpRequest.CreateGateway(_userId, 50m, 137m, [new(PendingItemType.Course, "course-1", 50m)]);
        topUp.ConfirmGateway("bank-1", "rrn-1", "SEP");
        _topUps.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);
        if (status.HasValue)
        {
            var purchase = CoursePurchase.Create(_userId, ProductSnapshot.Create("course-1", "دوره", 50m, ProductCatalogSources.TarhElahi));
            purchase.MarkDebited(Guid.NewGuid());
            if (status == CoursePurchaseStatus.NotificationSent) purchase.MarkNotificationSent();
            if (status == CoursePurchaseStatus.NotificationFailed) purchase.RecordNotificationFailure("خطای تحویل");
            _purchases.Setup(x => x.GetByBuyerAndCourseAsync(_userId, "course-1", It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        }
        var handler = new GetTopUpCheckoutQueryHandler(_current.Object, _topUps.Object, _purchases.Object);
        var response = await handler.Handle(new(topUp.Id.Value), CancellationToken.None);
        response.TopUpStatus.Should().Be("Confirmed");
        response.Courses.Single().PurchaseStatus.Should().Be(expected);
    }

    [Fact]
    public async Task DeliveredCourseStatus_RemainsReadableWhenCatalogIsUnavailable()
    {
        var purchase = CoursePurchase.Create(_userId, ProductSnapshot.Create("course-1", "دوره", 50m, ProductCatalogSources.TarhElahi));
        purchase.MarkDebited(Guid.NewGuid());
        purchase.MarkNotificationSent();
        _purchases.Setup(x => x.GetByBuyerAndCourseAsync(_userId, "course-1", It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        var handler = new GetCourseCheckoutQueryHandler(_current.Object, _catalog.Object, _wallets.Object, _settings.Object, _purchases.Object);
        (await handler.Handle(new("course-1"), CancellationToken.None)).PurchaseStatus.Should().Be(CheckoutStates.Completed);
        _catalog.Verify(x => x.GetCourseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
