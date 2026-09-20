using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.EventHandlers;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Events;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class TopUpConfirmedPurchaseHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahiClientMock = new();
    private readonly Mock<ICoursePurchaseRepository> _coursePurchaseRepoMock = new();
    private readonly Mock<IUserWalletProvisioningService> _walletProvisioningMock = new();
    private readonly Mock<IWalletRepository> _walletRepoMock = new();
    private readonly Mock<IRepository<LedgerEntry, LedgerEntryId>> _ledgerRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICoursePurchaseNotifier> _notifierMock = new();
    private readonly Mock<ILogger<TopUpConfirmedPurchaseHandler>> _loggerMock = new();

    private readonly TopUpConfirmedPurchaseHandler _handler;

    public TopUpConfirmedPurchaseHandlerTests()
    {
        _handler = new TopUpConfirmedPurchaseHandler(
            _userRepoMock.Object,
            _tarhElahiClientMock.Object,
            _coursePurchaseRepoMock.Object,
            _walletProvisioningMock.Object,
            _walletRepoMock.Object,
            _ledgerRepoMock.Object,
            _unitOfWorkMock.Object,
            _notifierMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCourseIsPurchasable_ShouldHonorFrozenSnapshot_AndExecutePurchase_AndDebitWallet_AndWriteBalancedLedgerEntries_AndNotify()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var course = new TarhElahiCourseDto
        {
            ExternalId = "course-abc-123",
            Title = "Mastering Architecture",
            PriceRial = 5_000_000m, // Drifted price in Strapi
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("course-abc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "course-abc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(1500m);

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        var savedLedgerEntries = new List<LedgerEntry>();
        _ledgerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => savedLedgerEntries.Add(entry))
            .Returns(Task.CompletedTask);

        CoursePurchase? savedPurchase = null;
        _coursePurchaseRepoMock
            .Setup(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()))
            .Callback<CoursePurchase, CancellationToken>((p, _) => savedPurchase = p)
            .Returns(Task.CompletedTask);

        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            1000m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "course-abc-123", 1_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        wallet.Balance.Should().Be(500m); // 1,500 - 1,000
        _walletRepoMock.Verify(w => w.Update(wallet), Times.Once);

        savedLedgerEntries.Should().HaveCount(2);
        var buyerEntry = savedLedgerEntries.Single(e => e.AccountId == userAccount.Id);
        var sysEntry = savedLedgerEntries.Single(e => e.AccountId == systemAccount.Id);

        buyerEntry.Amount.Should().Be(-1000m);
        sysEntry.Amount.Should().Be(1000m);
        buyerEntry.TransactionId.Should().Be(sysEntry.TransactionId);
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);

        savedPurchase.Should().NotBeNull();
        savedPurchase!.BuyerId.Should().Be(userId);
        savedPurchase.Status.Should().Be(CoursePurchaseStatus.Debited);
        savedPurchase.Snapshot.PriceInRialAtPurchaseTime.Should().Be(1_000_000m);
        savedPurchase.Snapshot.ConversionRateAtPurchaseTime.Should().Be(1_000m);
        savedPurchase.Snapshot.PriceInNoorAtPurchaseTime.Should().Be(1_000m);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notifierMock.Verify(n => n.NotifyAsync(
            savedPurchase,
            user.ExternalUserId,
            course,
            savedPurchase.Snapshot,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBasketHasMultipleCourses_ShouldPurchaseAllCoursesAtomically_DebitTotalFromWallet_AndWriteBalancedLedgerEntriesForEach()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var course1 = new TarhElahiCourseDto
        {
            ExternalId = "course-1",
            Title = "Course 1",
            PriceRial = 1_000_000m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var course2 = new TarhElahiCourseDto
        {
            ExternalId = "course-2",
            Title = "Course 2",
            PriceRial = 2_000_000m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("course-1", It.IsAny<CancellationToken>())).ReturnsAsync(course1);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("course-2", It.IsAny<CancellationToken>())).ReturnsAsync(course2);

        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(5000m);

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        var savedLedgerEntries = new List<LedgerEntry>();
        _ledgerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => savedLedgerEntries.Add(entry))
            .Returns(Task.CompletedTask);

        var savedPurchases = new List<CoursePurchase>();
        _coursePurchaseRepoMock
            .Setup(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()))
            .Callback<CoursePurchase, CancellationToken>((p, _) => savedPurchases.Add(p))
            .Returns(Task.CompletedTask);

        // Basket of 2 courses: 1,000 Noor + 2,000 Noor = 3,000 Noor total
        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            3000m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "course-1", 1_000_000m, 1_000m),
                new(PendingItemType.Course, "course-2", 2_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        wallet.Balance.Should().Be(2000m); // 5000 - 3000
        savedPurchases.Should().HaveCount(2);
        savedLedgerEntries.Should().HaveCount(4); // 2 pairs of debit/credit
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), user.ExternalUserId, It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenCourseDoesNotExistInTarhElahi_ShouldDoNothing_AndKeepWalletCredited()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "deleted-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("deleted-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TarhElahiCourseDto?)null);

        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            1000m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "deleted-course", 1_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _coursePurchaseRepoMock.Verify(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()), Times.Never);
        _ledgerRepoMock.Verify(l => l.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(w => w.Update(It.IsAny<WalletEntity>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCourseIsUnpublishedOrUnavailable_ShouldDoNothing_AndKeepWalletCredited()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "unpublished-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var course = new TarhElahiCourseDto
        {
            ExternalId = "unpublished-course",
            Title = "Unpublished Course",
            PriceRial = 1_000_000m,
            Published = false,
            Available = false,
            Source = ProductCatalogSources.TarhElahi
        };

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("unpublished-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            1000m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "unpublished-course", 1_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _coursePurchaseRepoMock.Verify(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()), Times.Never);
        _ledgerRepoMock.Verify(l => l.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(w => w.Update(It.IsAny<WalletEntity>()), Times.Never);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPurchaseAlreadyExists_ShouldReturnEarly_WithoutDoubleDebiting_ForIdempotency()
    {
        // Arrange
        var userId = UserId.New();

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "course-already-owned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            1000m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "course-already-owned", 1_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _tarhElahiClientMock.Verify(c => c.GetCourseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _coursePurchaseRepoMock.Verify(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()), Times.Never);
        _ledgerRepoMock.Verify(l => l.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(w => w.Update(It.IsAny<WalletEntity>()), Times.Never);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWalletBalanceIsInsufficientForFrozenNoorPrice_ShouldDoNothing_AndKeepWalletCredited()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var course = new TarhElahiCourseDto
        {
            ExternalId = "course-expensive",
            Title = "Expensive Course",
            PriceRial = 1_000_000m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("course-expensive", It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "course-expensive", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var userAccount = Account.CreateUserAccount(userId);
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(200m);

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        var notification = new TopUpConfirmed(
            TopUpRequestId.New(),
            userId,
            200m,
            new List<PendingItemSnapshot>
            {
                new(PendingItemType.Course, "course-expensive", 1_000_000m, 1_000m)
            });

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        wallet.Balance.Should().Be(200m);
        _coursePurchaseRepoMock.Verify(r => r.AddAsync(It.IsAny<CoursePurchase>(), It.IsAny<CancellationToken>()), Times.Never);
        _ledgerRepoMock.Verify(l => l.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _walletRepoMock.Verify(w => w.Update(It.IsAny<WalletEntity>()), Times.Never);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

