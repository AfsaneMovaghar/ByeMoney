using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Events;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class ConfirmTopUpCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldConfirmPendingRequest_AndWriteBalancedLedgerEntries_WhenFirstTimeConfirmation()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 100_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        walletProvisioningMock
            .Setup(s => s.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        var savedLedgerEntries = new List<LedgerEntry>();
        ledgerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => savedLedgerEntries.Add(entry))
            .Returns(Task.CompletedTask);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "gateway_tx_777", amount),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        topUp.ExternalTransactionId.Should().Be("gateway_tx_777");

        // بررسی تراز بودن دفتر کل (Double-entry Zero-Net invariant)
        savedLedgerEntries.Should().HaveCount(2);

        var userEntry = savedLedgerEntries.Single(e => e.AccountId == userAccount.Id);
        var systemEntry = savedLedgerEntries.Single(e => e.AccountId == systemAccount.Id);

        userEntry.Amount.Should().Be(amount);
        systemEntry.Amount.Should().Be(-amount);
        userEntry.TransactionId.Should().Be(systemEntry.TransactionId);
        userEntry.ReferenceType.Should().Be(LedgerReferenceType.TopUp);
        systemEntry.ReferenceType.Should().Be(LedgerReferenceType.TopUp);

        // مجموع تراکنش‌ها باید دقیقاً صفر باشد
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);

        // اسنپ‌شات کیف پول کاربر باید به میزان شارژ افزایش یافته باشد
        wallet.Balance.Should().Be(amount);
        walletRepoMock.Verify(r => r.Update(wallet), Times.Once);

        // تغییرات باید ذخیره شده باشند
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Event should NOT be published because pending fields are null
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPublishTopUpConfirmedEvent_WhenPendingFieldsAreSet()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 100_000m;
        var topUp = TopUpRequest.Create(
            userId,
            amount,
            PaymentMethod.Gateway,
            pendingItemType: PendingItemType.Course,
            pendingItemExternalId: "course-uuid-999",
            pendingPriceSnapshot: 1_000_000m,
            pendingRateSnapshot: 1_000m);

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        walletProvisioningMock
            .Setup(s => s.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "tx-pending-123", amount),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        publisherMock.Verify(p => p.Publish(
            It.Is<TopUpConfirmed>(e =>
                e.TopUpRequestId == topUp.Id &&
                e.UserId == userId &&
                e.ConfirmedAmount == amount &&
                e.PendingItemType == PendingItemType.Course &&
                e.PendingItemExternalId == "course-uuid-999" &&
                e.PendingPriceSnapshot == 1_000_000m &&
                e.PendingRateSnapshot == 1_000m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WithoutWritingNewLedgerEntries_WhenRetryWithSameExternalTransactionId()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 50_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);
        topUp.Confirm("tx_idempotent_123");

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "tx_idempotent_123", amount),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ledgerRepoMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WithoutWritingLedgerEntries_WhenRetryWithDifferentExternalTransactionId()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 50_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);
        topUp.Confirm("tx_original_123");

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "tx_different_456", amount),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Conflict);
        ledgerRepoMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WithoutConfirmingOrWritingLedgerEntries_WhenConfirmedAmountMismatches()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 100_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "tx_123", 50_000m), // Mismatch
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Pending); // Confirm was never called
        topUp.ExternalTransactionId.Should().BeNull();
        ledgerRepoMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WithoutWritingLedgerEntries_WhenTopUpRequestIsRejected()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 100_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);
        topUp.Reject("Invalid receipt");

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id, "tx_123", amount),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Rejected);
        ledgerRepoMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTopUpRequestDoesNotExist()
    {
        // Arrange
        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var walletProvisioningMock = new Mock<IUserWalletProvisioningService>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publisherMock = new Mock<IPublisher>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<TopUpRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopUpRequest?)null);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            walletProvisioningMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object,
            publisherMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(TopUpRequestId.New(), "tx_123", 100_000m),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
        ledgerRepoMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.Publish(It.IsAny<TopUpConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

