using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class ConfirmTopUpCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateBalancedDoubleEntryLedgerEntriesAndCreditWallet()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 100_000m;
        var topUp = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway);

        var userAccount = Account.CreateUserAccount(userId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, userId);

        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var accountRepoMock = new Mock<IAccountRepository>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        accountRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAccount);

        accountRepoMock
            .Setup(r => r.GetSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        walletRepoMock
            .Setup(r => r.GetByAccountIdAsync(userAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var savedLedgerEntries = new List<LedgerEntry>();
        ledgerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => savedLedgerEntries.Add(entry))
            .Returns(Task.CompletedTask);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            accountRepoMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new ConfirmTopUpCommand(topUp.Id.Value, "gateway_tx_777"),
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        topUp.ExternalTransactionId.Should().Be("gateway_tx_777");

        // بررسی تراز بودن دفتر کل (Double-entry Zero-Net invariant)
        savedLedgerEntries.Should().HaveCount(2);

        var userEntry = savedLedgerEntries.Single(e => e.AccountId == userAccount.Id);
        var systemEntry = savedLedgerEntries.Single(e => e.AccountId == systemAccount.Id);

        userEntry.Amount.Should().Be(amount);
        systemEntry.Amount.Should().Be(-amount);
        userEntry.TransactionId.Should().Be(systemEntry.TransactionId);

        // مجموع تراکنش‌ها باید دقیقاً صفر باشد
        savedLedgerEntries.Sum(e => e.Amount).Should().Be(0m);

        // اسنپ‌شات کیف پول کاربر باید به میزان شارژ افزایش یافته باشد
        wallet.Balance.Should().Be(amount);

        // تغییرات باید ذخیره شده باشند
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenTopUpRequestDoesNotExist()
    {
        // Arrange
        var topUpRepoMock = new Mock<ITopUpRequestRepository>();
        var accountRepoMock = new Mock<IAccountRepository>();
        var walletRepoMock = new Mock<IWalletRepository>();
        var ledgerRepoMock = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        topUpRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<TopUpRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopUpRequest?)null);

        var handler = new ConfirmTopUpCommandHandler(
            topUpRepoMock.Object,
            accountRepoMock.Object,
            walletRepoMock.Object,
            ledgerRepoMock.Object,
            unitOfWorkMock.Object);

        // Act
        var act = () => handler.Handle(
            new ConfirmTopUpCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

