using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Services;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;
using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class AdminCartOrderTests
{
    private const string FreeReason = "جبران قطعی پخش زنده";
    private static readonly Guid AdminUserId = Guid.NewGuid();

    private static readonly CartItemInput[] CourseAndChapter =
    [
        new("course-a", ProductItemType.Course),
        new("chapter-b", ProductItemType.CourseChapter)
    ];

    private static readonly CartQuoteItem[] CourseAndChapterQuote =
    [
        new("course-a", "دوره", 300, 50, 0, 250),
        new("chapter-b", "فصل", 120, 0, 0, 120, ProductItemType.CourseChapter, "parent-course")
    ];

    [Fact]
    public void FreeAdminCart_KeepsQuotedPriceForDisplayOnly_AndZeroesEveryCartShare()
    {
        var buyer = UserId.New();
        var cart = Cart.CreateAdminOrder(Guid.NewGuid(), buyer.Value, "buyer", CourseAndChapter, AdminUserId, FreeReason, FinancialScope.TarheElahi);

        cart.SetQuote(CourseAndChapterQuote, shippingAmountInNoor: 40);

        cart.OriginalTotal.Should().Be(420);
        cart.Total.Should().Be(0);
        cart.ShippingAmountInNoor.Should().Be(0);
        cart.Items.Should().OnlyContain(item => item.AllocatedAmountInNoor == 0);
        cart.Items.Select(item => item.PriceInNoor).Should().BeEquivalentTo([300m, 120m]);
    }

    [Fact]
    public void FreeAdminCartPayment_MarksEveryItemFree_WithoutLedgerOrWalletEffect()
    {
        var buyer = UserId.New();
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.Credit(70, LedgerBalanceType.Regular);
        wallet.Credit(30, LedgerBalanceType.Gift);
        var cart = Cart.CreateAdminOrder(Guid.NewGuid(), buyer.Value, "buyer", CourseAndChapter, AdminUserId, FreeReason, FinancialScope.TarheElahi);
        cart.SetQuote(CourseAndChapterQuote);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), system: null);

        payment.LedgerEntries.Should().BeEmpty();
        wallet.TotalBalance.Should().Be(100);
        payment.Purchases.Should().HaveCount(2);
        payment.Purchases.Should().OnlyContain(purchase =>
            purchase.IsFree &&
            purchase.FreeReason == FreeReason &&
            purchase.Status == CoursePurchaseStatus.Debited &&
            purchase.Snapshot.PriceInNoorAtPurchaseTime == 0 &&
            purchase.RegularAmountInNoor == 0 &&
            purchase.GiftAmountInNoor == 0);
        cart.Status.Should().Be(CartStatus.Paid);
    }

    [Fact]
    public void PaidAdminCartPayment_DebitsTotalOnce_BalancesLedger_AndKeepsChapterParent()
    {
        var buyer = UserId.New();
        var account = Account.CreateUserAccount(buyer, FinancialScope.TarheElahi);
        var wallet = Wallet.Create(account.Id, buyer, FinancialScope.TarheElahi);
        wallet.Credit(1000, LedgerBalanceType.Regular);
        var cart = Cart.CreateAdminOrder(Guid.NewGuid(), buyer.Value, "buyer", CourseAndChapter, AdminUserId, null, FinancialScope.TarheElahi);
        cart.SetQuote(CourseAndChapterQuote);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount(FinancialScope.TarheElahi));

        cart.Total.Should().Be(370);
        wallet.TotalBalance.Should().Be(630);
        payment.LedgerEntries.Sum(entry => entry.Amount).Should().Be(0);
        (-payment.LedgerEntries.Where(entry => entry.Amount < 0).Sum(entry => entry.Amount)).Should().Be(cart.Total);
        payment.Purchases.Should().OnlyContain(purchase => !purchase.IsFree && purchase.FreeReason == null);
        var chapter = payment.Purchases.Single(purchase => purchase.Snapshot.ItemType == ProductItemType.CourseChapter);
        chapter.Snapshot.ExternalProductId.Should().Be("chapter-b");
        chapter.Snapshot.ExternalParentId.Should().Be("parent-course");
    }

    [Fact]
    public void AdminMarkers_DistinguishRepeatedCartIdFromDifferentModeOrBuyerCart()
    {
        var id = Guid.NewGuid();
        var buyer = Guid.NewGuid();
        var free = Cart.CreateAdminOrder(id, buyer, "buyer", CourseAndChapter, AdminUserId, FreeReason, FinancialScope.TarheElahi);

        free.HasSameAdminOrder(Cart.CreateAdminOrder(id, buyer, "buyer", CourseAndChapter, AdminUserId, FreeReason, FinancialScope.TarheElahi))
            .Should().BeTrue();
        free.HasSameAdminOrder(Cart.CreateAdminOrder(id, buyer, "buyer", CourseAndChapter, AdminUserId, null, FinancialScope.TarheElahi))
            .Should().BeFalse();
        free.HasSameAdminOrder(Cart.Create(id, buyer, "buyer", CourseAndChapter, null, FinancialScope.TarheElahi))
            .Should().BeFalse();
        free.CouponCode.Should().BeNull();
    }

    [Fact]
    public void Validator_RejectsProductItem()
    {
        var command = Command(items: [new CartItemInput("book", ProductItemType.Product)]);

        var result = new AdminCartOrderValidator().TestValidate(command);

        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(AdminCartOrderErrors.ItemTypeNotAllowed);
    }

    [Fact]
    public void Validator_RequiresReasonOnlyForFreeOrder()
    {
        var validator = new AdminCartOrderValidator();

        validator.TestValidate(Command(isFree: true, freeReason: "  "))
            .ShouldHaveValidationErrorFor(x => x.FreeReason)
            .WithErrorMessage(AdminCartOrderErrors.FreeReasonRequired);
        validator.TestValidate(Command(isFree: false, freeReason: FreeReason))
            .ShouldHaveValidationErrorFor(x => x.FreeReason)
            .WithErrorMessage(AdminCartOrderErrors.FreeReasonNotAllowed);
        validator.TestValidate(Command(isFree: true, freeReason: FreeReason)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(Command()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Shortfall_Returns409WithStructuredCode_AndReleasesReservationWithoutPayment()
    {
        var buyer = User.CreateInitial("beneficiary");
        var cart = Cart.CreateAdminOrder(Guid.NewGuid(), buyer.Id.Value, buyer.ExternalUserId, CourseAndChapter, AdminUserId, null, FinancialScope.TarheElahi);
        cart.SetQuote(CourseAndChapterQuote);
        var store = new Mock<ICartStore>();
        var pricing = new Mock<ICartPricingClient>();
        store.Setup(x => x.CreateAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Cart>.Success(cart));
        store.Setup(x => x.FindAsync(cart.Id, buyer.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        store.Setup(x => x.PayAsync(cart.Id, buyer.Id.Value, cart.ReservationId, It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(Result<Cart>.Failure(CartErrors.InsufficientBalance, CartErrorCodes.InsufficientBalance));
        store.Setup(x => x.CancelAsync(cart.Id, buyer.Id.Value, It.IsAny<CancellationToken>()))
            .Callback(() => cart.Fail())
            .ReturnsAsync(() => Result<Cart>.Success(cart));
        pricing.Setup(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await Handler(buyer, store, pricing).Handle(Command(cartId: cart.Id), default);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(PurchaseErrorCodes.InsufficientNoorBalance);
        cart.Status.Should().Be(CartStatus.Failed);
        store.Verify(x => x.CancelAsync(cart.Id, buyer.Id.Value, It.IsAny<CancellationToken>()), Times.Once);
        pricing.Verify(x => x.TransitionAsync(
            It.Is<CartTransitionRequest>(request => request.CartId == cart.ReservationId),
            "release",
            It.IsAny<CancellationToken>()), Times.Once);
        pricing.Verify(x => x.TransitionAsync(It.IsAny<CartTransitionRequest>(), "commit", It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AdminCartOrderCommand Command(
        Guid? cartId = null,
        CartItemInput[]? items = null,
        bool isFree = false,
        string? freeReason = null) =>
        new(cartId ?? Guid.NewGuid(), "beneficiary", items ?? CourseAndChapter, isFree, freeReason);

    private static AdminCartOrderCommandHandler Handler(User buyer, Mock<ICartStore> store, Mock<ICartPricingClient> pricing)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(buyer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(buyer);
        var provisioning = new Mock<IUserProvisioningService>();
        provisioning.Setup(x => x.EnsureUserFromExternalAsync(buyer.ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(buyer, Created: false));
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(137);
        var carts = new CartService(store.Object, pricing.Object, users.Object, Mock.Of<IWalletRepository>(),
            settings.Object, Mock.Of<ICoursePurchaseRepository>(), Mock.Of<ITopUpRequestRepository>());
        var sync = new UserProfileSyncService(provisioning.Object, Mock.Of<ITarhElahiIntegrationClient>(),
            users.Object, Mock.Of<IUnitOfWork>(), Mock.Of<ILogger<UserProfileSyncService>>());
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(AdminUserId);
        return new AdminCartOrderCommandHandler(carts, sync, currentUser.Object);
    }
}
