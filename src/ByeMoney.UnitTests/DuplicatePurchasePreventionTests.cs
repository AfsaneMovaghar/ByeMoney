using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Settings;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class DuplicatePurchasePreventionTests
{
    [Fact]
    public async Task PrepareCart_WhenBuyerAlreadyOwnsCourseInByeMoney_ShouldReturnConflictAndNotCreateCart()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);

        var storeMock = new Mock<ICartStore>();
        var pricingMock = new Mock<ICartPricingClient>();
        var userRepoMock = new Mock<IUserRepository>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var settingsMock = new Mock<ISystemSettingRepository>();
        var purchasesMock = new Mock<ICoursePurchaseRepository>();
        var topUpsMock = new Mock<ITopUpRequestRepository>();

        userRepoMock.Setup(r => r.GetByIdAsync(new UserId(buyerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Buyer already owns "course-123" in ByeMoney
        purchasesMock.Setup(r => r.HasPurchasedAnyAsync(
                new UserId(buyerId),
                It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("course-123")),
                It.IsAny<IReadOnlyCollection<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CartService(
            storeMock.Object,
            pricingMock.Object,
            userRepoMock.Object,
            walletRepoMock.Object,
            settingsMock.Object,
            purchasesMock.Object,
            topUpsMock.Object);

        // Act
        var result = await service.PrepareAsync(buyerId, cartId, new[] { "course-123" }, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        // Cart should not be created in store
        storeMock.Verify(s => s.CreateAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        // Strapi pricing should not even be requested
        pricingMock.Verify(p => p.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareCart_WhenStrapiRejectsForLegacyOwnership_ShouldReturnConflict()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var user = User.CreateFromStrapi("strapi-user-legacy", "09123456789", confirmed: true, blocked: false);

        var storeMock = new Mock<ICartStore>();
        var pricingMock = new Mock<ICartPricingClient>();
        var userRepoMock = new Mock<IUserRepository>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var settingsMock = new Mock<ISystemSettingRepository>();
        var purchasesMock = new Mock<ICoursePurchaseRepository>();
        var topUpsMock = new Mock<ITopUpRequestRepository>();

        userRepoMock.Setup(r => r.GetByIdAsync(new UserId(buyerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Not in ByeMoney DB
        purchasesMock.Setup(r => r.HasPurchasedAnyAsync(
                It.IsAny<UserId>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cart = Cart.Create(cartId, buyerId, user.ExternalUserId, new[] { "legacy-course" }, null);
        storeMock.Setup(s => s.CreateAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Cart>.Success(cart));

        // Strapi returns Conflict (409 ALREADY_OWNED)
        pricingMock.Setup(p => p.PrepareAsync(It.IsAny<CartQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CartQuote>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));

        var service = new CartService(
            storeMock.Object,
            pricingMock.Object,
            userRepoMock.Object,
            walletRepoMock.Object,
            settingsMock.Object,
            purchasesMock.Object,
            topUpsMock.Object);

        // Act
        var result = await service.PrepareAsync(buyerId, cartId, new[] { "legacy-course" }, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        // Cart should be failed and synchronized to release any partial state
        storeMock.Verify(s => s.FailAsync(cartId, buyerId, false, cart.ReservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseCart_WhenStrapiCheckFailsWithConflict_ShouldFailCartAndNotDebit()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);

        var storeMock = new Mock<ICartStore>();
        var pricingMock = new Mock<ICartPricingClient>();
        var userRepoMock = new Mock<IUserRepository>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var settingsMock = new Mock<ISystemSettingRepository>();
        var purchasesMock = new Mock<ICoursePurchaseRepository>();
        var topUpsMock = new Mock<ITopUpRequestRepository>();

        var cart = Cart.Create(cartId, buyerId, user.ExternalUserId, new[] { "c1" }, null);
        cart.SetQuote(new[] { new CartQuoteItem("c1", "Title", 100m, 0m, 0m, 100m) }, 0m, null);

        storeMock.Setup(s => s.FindAsync(cartId, buyerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        // Strapi check returns Conflict (e.g. legacy enrolled course detected)
        pricingMock.Setup(p => p.TransitionAsync(It.IsAny<CartTransitionRequest>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict));

        var service = new CartService(
            storeMock.Object,
            pricingMock.Object,
            userRepoMock.Object,
            walletRepoMock.Object,
            settingsMock.Object,
            purchasesMock.Object,
            topUpsMock.Object);

        // Act
        var result = await service.PurchaseAsync(buyerId, cartId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        // PayAsync should NEVER be called
        storeMock.Verify(s => s.PayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseCourses_WhenBuyerAlreadyOwnsCourse_ShouldReturnConflictWithoutDebiting()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);

        var userRepoMock = new Mock<IUserRepository>();
        var tarhElahiClientMock = new Mock<ITarhElahiIntegrationClient>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var coursePurchaseRepoMock = new Mock<ICoursePurchaseRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var notifierMock = new Mock<ICoursePurchaseNotifier>();
        var settingsMock = new Mock<ISystemSettingRepository>();

        userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Already owned in ByeMoney
        coursePurchaseRepoMock.Setup(r => r.HasPurchasedAnyAsync(
                userId,
                It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("already-owned-course")),
                It.IsAny<IReadOnlyCollection<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new PurchaseCoursesCommandHandler(
            userRepoMock.Object,
            tarhElahiClientMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            new ByeMoney.Application.Modules.Wallet.Services.WalletPostingService(ledgerRepoMock.Object, walletRepoMock.Object),
            coursePurchaseRepoMock.Object,
            unitOfWorkMock.Object,
            notifierMock.Object,
            settingsMock.Object);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "already-owned-course" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(PurchaseErrorCodes.AlreadyPurchased);
        // Wallet should NOT be debited
        walletProvisioningMock.Verify(w => w.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
        // SaveChangesAsync should NOT be called
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // Notifier should NOT be called
        notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseCourses_ConcurrentDuplicateRequests_SecondRequestShouldReturnConflictWithoutDoubleCharging()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);

        var course = new TarhElahiCourseDto
        {
            ExternalId = "concurrent-course",
            Title = "Concurrent Course",
            PriceNoor = 500m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = Wallet.Create(userAccount.Id, userId);
        wallet.ApplyCredit(1_000m);

        var userRepoMock = new Mock<IUserRepository>();
        var tarhElahiClientMock = new Mock<ITarhElahiIntegrationClient>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var coursePurchaseRepoMock = new Mock<ICoursePurchaseRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var notifierMock = new Mock<ICoursePurchaseNotifier>();
        var settingsMock = new Mock<ISystemSettingRepository>();

        userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        tarhElahiClientMock.Setup(c => c.GetCourseAsync("concurrent-course", It.IsAny<CancellationToken>())).ReturnsAsync(course);

        walletProvisioningMock.Setup(w => w.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));
        walletProvisioningMock.Setup(w => w.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        // Pre-check passes
        coursePurchaseRepoMock.Setup(r => r.HasPurchasedAnyAsync(
                userId,
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // ExecutePurchaseTransactionAsync simulates duplicate unique conflict on second request
        var callCount = 0;
        coursePurchaseRepoMock
            .Setup(r => r.ExecutePurchaseTransactionAsync(userId, It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<UserId, Func<Task>, CancellationToken>(async (_, action, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    await action();
                    return Result.Success();
                }

                // Second concurrent request hits unique constraint
                return Result.Conflict(CartErrors.Conflict, PurchaseErrorCodes.AlreadyPurchased);
            });

        var handler = new PurchaseCoursesCommandHandler(
            userRepoMock.Object,
            tarhElahiClientMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            new ByeMoney.Application.Modules.Wallet.Services.WalletPostingService(ledgerRepoMock.Object, walletRepoMock.Object),
            coursePurchaseRepoMock.Object,
            unitOfWorkMock.Object,
            notifierMock.Object,
            settingsMock.Object);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "concurrent-course" });

        // Act - Request 1
        var result1 = await handler.Handle(command, CancellationToken.None);
        // Act - Request 2
        var result2 = await handler.Handle(command, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeFalse();
        result2.ErrorCode.Should().Be(PurchaseErrorCodes.AlreadyPurchased);

        // Only one notification should be sent
        notifierMock.Verify(n => n.NotifyAsync(
            It.IsAny<CoursePurchase>(),
            user.ExternalUserId,
            It.IsAny<ProductSnapshot>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

