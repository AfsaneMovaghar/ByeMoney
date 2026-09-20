using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class PurchaseCoursesCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahiClientMock = new();
    private readonly Mock<ISystemSettingRepository> _settingRepoMock = new();
    private readonly Mock<IUserWalletProvisioningService> _walletProvisioningMock = new();
    private readonly Mock<IWalletRepository> _walletRepoMock = new();
    private readonly Mock<IRepository<LedgerEntry, LedgerEntryId>> _ledgerRepoMock = new();
    private readonly Mock<ICoursePurchaseRepository> _coursePurchaseRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICoursePurchaseNotifier> _notifierMock = new();

    private readonly PurchaseCoursesCommandHandler _handler;

    public PurchaseCoursesCommandHandlerTests()
    {
        _handler = new PurchaseCoursesCommandHandler(
            _userRepoMock.Object,
            _tarhElahiClientMock.Object,
            _settingRepoMock.Object,
            _walletProvisioningMock.Object,
            _walletRepoMock.Object,
            _ledgerRepoMock.Object,
            _coursePurchaseRepoMock.Object,
            _unitOfWorkMock.Object,
            _notifierMock.Object);
    }

    [Fact]
    public async Task Handle_BasketOfTwoValidCourses_ShouldDebitWalletTotal_WriteBalancedLedgerEntriesForEach_AndTriggerNotifications()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        var conversionRate = 1000m;

        var course1 = new TarhElahiCourseDto
        {
            ExternalId = "course-1",
            Title = "Course 1",
            PriceRial = 2_500_000m, // 2,500 Noor
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var course2 = new TarhElahiCourseDto
        {
            ExternalId = "course-2",
            Title = "Course 2",
            PriceRial = 1_500_000m, // 1,500 Noor
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(10_000m);

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(conversionRate);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("course-1", It.IsAny<CancellationToken>())).ReturnsAsync(course1);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("course-2", It.IsAny<CancellationToken>())).ReturnsAsync(course2);

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

        _notifierMock
            .Setup(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "course-1", "course-2" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPriceInNoor.Should().Be(4_000m); // 2,500 + 1,500
        result.Value.Items.Should().HaveCount(2);

        // 1. Wallet debited by total
        wallet.Balance.Should().Be(6_000m); // 10,000 - 4,000
        _walletRepoMock.Verify(w => w.Update(wallet), Times.Once);

        // 2. Exactly 2 CoursePurchase rows created with status Debited
        savedPurchases.Should().HaveCount(2);
        savedPurchases[0].Status.Should().Be(CoursePurchaseStatus.Debited);
        savedPurchases[1].Status.Should().Be(CoursePurchaseStatus.Debited);

        // 3. Exactly 4 Ledger entries (one debit/credit pair per course)
        savedLedgerEntries.Should().HaveCount(4);
        var debits = savedLedgerEntries.Where(e => e.AccountId == userAccount.Id).ToList();
        var credits = savedLedgerEntries.Where(e => e.AccountId == systemAccount.Id).ToList();
        debits.Should().HaveCount(2);
        credits.Should().HaveCount(2);
        debits.Sum(d => d.Amount).Should().Be(-4_000m);
        credits.Sum(c => c.Amount).Should().Be(4_000m);
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);

        // All entries share the same transaction id
        savedLedgerEntries.Select(e => e.TransactionId).Distinct().Should().ContainSingle();

        // 4. Persistence in one atomic transaction
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // 5. Outbound webhook notifications called once per course
        _notifierMock.Verify(n => n.NotifyAsync(
            It.IsAny<CoursePurchase>(),
            user.ExternalUserId,
            It.IsAny<TarhElahiCourseDto>(),
            It.IsAny<ProductSnapshot>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_SingleCourseBasket_ShouldSucceedAtomically()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        var conversionRate = 1000m;

        var course = new TarhElahiCourseDto
        {
            ExternalId = "single-course",
            Title = "Single Course",
            PriceRial = 1_000_000m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(2_000m);

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(conversionRate);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("single-course", It.IsAny<CancellationToken>())).ReturnsAsync(course);

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

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "single-course" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPriceInNoor.Should().Be(1_000m);
        wallet.Balance.Should().Be(1_000m);
        savedLedgerEntries.Should().HaveCount(2);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), user.ExternalUserId, course, It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUnitOfWorkThrowsOnDuplicate_ShouldNotNotify()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        var conversionRate = 1000m;

        var course = new TarhElahiCourseDto
        {
            ExternalId = "duplicate-course",
            Title = "Duplicate Course",
            PriceRial = 1_000_000m,
            Published = true,
            Available = true,
            Source = ProductCatalogSources.TarhElahi
        };

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(2_000m);

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(conversionRate);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("duplicate-course", It.IsAny<CancellationToken>())).ReturnsAsync(course);

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        // Simulate database unique constraint violation on SaveChangesAsync
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Duplicate key violation"));

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "duplicate-course" });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _notifierMock.Verify(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
