using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
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

public class TopUpSettlementServiceTests
{
    [Fact]
    public async Task SettleAsync_CreditsBeneficiaryAndRecordsBalancedEntriesWithActor()
    {
        var beneficiaryId = UserId.New();
        var actorId = UserId.New();
        var topUp = TopUpRequest.CreateAdminCardToCard(beneficiaryId, actorId, 100_000m, 10_000m, "receipt", "key");
        var userAccount = Account.CreateUserAccount(beneficiaryId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, beneficiaryId);
        var provisioning = new Mock<IUserWalletProvisioningService>();
        provisioning.Setup(x => x.GetOrCreateUserWalletAsync(beneficiaryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));
        provisioning.Setup(x => x.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(systemAccount);
        var walletRepository = new Mock<IWalletRepository>();
        var ledgerRepository = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        var entries = new List<LedgerEntry>();
        ledgerRepository.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => entries.Add(entry)).Returns(Task.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new TopUpSettlementService(provisioning.Object, walletRepository.Object, ledgerRepository.Object, unitOfWork.Object);

        await service.SettleAsync(topUp);

        entries.Should().HaveCount(2);
        entries.Sum(x => x.Amount).Should().Be(0m);
        entries.Should().OnlyContain(x => x.PerformedByUserId == actorId);
        entries.Select(x => x.TransactionId).Distinct().Should().ContainSingle();
        wallet.Balance.Should().Be(10m);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}