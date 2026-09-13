using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;
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

public class PurchaseCourseCommandHandlerTests
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

    private readonly PurchaseCourseCommandHandler _handler;

    public PurchaseCourseCommandHandlerTests()
    {
        _handler = new PurchaseCourseCommandHandler(
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
    public async Task Handle_HappyPath_ShouldDebitWallet_WriteTwoBalancedLedgerEntries_AndTriggerNotification()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        var courseId = "course-doc-777";
        var priceRial = 2_500_000m;
        var conversionRate = 1000m;
        var expectedPriceNoor = 2500m; // 2_500_000 / 1000

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(10_000m); // initial balance 10,000 Noor

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var courseDto = new TarhElahiCourseDto
        {
            Source = ProductCatalogSources.TarhElahi,
            Type = "course",
            ExternalId = courseId,
            Title = "دوره جامع معماری",
            Slug = "architecture-course",
            PriceRial = priceRial,
            Published = true,
            Available = true,
            UpdatedAt = DateTime.UtcNow
        };

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(courseDto);

        _settingRepoMock
            .Setup(s => s.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversionRate);

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

        _notifierMock
            .Setup(n => n.NotifyAsync(It.IsAny<CoursePurchase>(), It.IsAny<string>(), It.IsAny<TarhElahiCourseDto>(), It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<CoursePurchase, string, TarhElahiCourseDto, ProductSnapshot, CancellationToken>((p, _, _, _, _) => p.MarkNotificationSent())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new PurchaseCourseCommand(userId.Value, courseId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PriceInNoor.Should().Be(expectedPriceNoor);
        result.Value.ExternalCourseId.Should().Be(courseId);
        result.Value.Status.Should().Be(CoursePurchaseStatus.NotificationSent.ToString());

        // Wallet debited
        wallet.Balance.Should().Be(7500m); // 10,000 - 2,500
        _walletRepoMock.Verify(r => r.Update(wallet), Times.Once);

        // Two balanced ledger entries
        savedLedgerEntries.Should().HaveCount(2);
        var buyerEntry = savedLedgerEntries.Single(e => e.AccountId == userAccount.Id);
        var systemEntry = savedLedgerEntries.Single(e => e.AccountId == systemAccount.Id);

        buyerEntry.Amount.Should().Be(-expectedPriceNoor);
        systemEntry.Amount.Should().Be(expectedPriceNoor);
        buyerEntry.TransactionId.Should().Be(systemEntry.TransactionId);
        buyerEntry.ReferenceType.Should().Be(LedgerReferenceType.Purchase);
        systemEntry.ReferenceType.Should().Be(LedgerReferenceType.Purchase);

        // Zero-net balance invariant
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);

        // Purchase entity checks
        savedPurchase.Should().NotBeNull();
        savedPurchase!.LedgerTransactionId.Should().Be(buyerEntry.TransactionId);
        savedPurchase.Snapshot.PriceInRialAtPurchaseTime.Should().Be(priceRial);
        savedPurchase.Snapshot.ConversionRateAtPurchaseTime.Should().Be(conversionRate);
        savedPurchase.Snapshot.PriceInNoorAtPurchaseTime.Should().Be(expectedPriceNoor);

        // Notifier invoked with correct arguments
        _notifierMock.Verify(n => n.NotifyAsync(
            savedPurchase,
            "strapi-user-1",
            courseDto,
            It.Is<ProductSnapshot>(s => s.PriceInNoorAtPurchaseTime == expectedPriceNoor),
            It.IsAny<CancellationToken>()), Times.Once);

        // Unit of work saved changes for financial commit
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
