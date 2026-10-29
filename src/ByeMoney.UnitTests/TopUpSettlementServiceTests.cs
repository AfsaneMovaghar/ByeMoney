using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class TopUpSettlementServiceTests
{
    private static (TopUpSettlementService Service, Mock<IUnitOfWork> UnitOfWork,
        List<LedgerEntry> Entries, WalletEntity Wallet) CreateService(TopUpRequest topUp)
    {
        var userAccount = Account.CreateUserAccount(topUp.UserId);
        var systemAccount = Account.CreateSystemAccount();
        var wallet = WalletEntity.Create(userAccount.Id, topUp.UserId);
        var provisioning = new Mock<IUserWalletProvisioningService>();
        provisioning.Setup(x => x.GetOrCreateUserWalletAsync(topUp.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));
        provisioning.Setup(x => x.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemAccount);

        var entries = new List<LedgerEntry>();
        var ledgerRepository = new Mock<IRepository<LedgerEntry, LedgerEntryId>>();
        ledgerRepository.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => entries.Add(entry))
            .Returns(Task.CompletedTask);

        var postingService = new WalletPostingService(ledgerRepository.Object, new Mock<IWalletRepository>().Object);
        var noorCreditService = new NoorCreditService(provisioning.Object, postingService);

        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new TopUpSettlementService(noorCreditService, unitOfWork.Object);
        return (service, unitOfWork, entries, wallet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("receipt")]
    public async Task SettleAsync_CreditsBeneficiaryAndRecordsBalancedEntriesWithActor(string? receiptId)
    {
        var beneficiaryId = UserId.New();
        var actorId = UserId.New();
        var topUp = TopUpRequest.CreateAdminCardToCard(beneficiaryId, actorId, 10_000m, 10_000m, receiptId, "key");
        var (service, unitOfWork, entries, wallet) = CreateService(topUp);

        await service.SettleAsync(topUp);

        entries.Should().HaveCount(2);
        entries.Sum(x => x.Amount).Should().Be(0m);
        entries.Should().OnlyContain(x => x.PerformedByUserId == actorId);
        entries.Select(x => x.TransactionId).Distinct().Should().ContainSingle();
        wallet.WithdrawableBalance.Should().Be(10m);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SettleAsync_UsesTopUpClassificationAndKeepsGiftBalanceUntouched()
    {
        var topUp = TopUpRequest.CreateAdminCardToCard(UserId.New(), UserId.New(), 10_000m, 10_000m, null, "key");
        var (service, _, entries, wallet) = CreateService(topUp);

        await service.SettleAsync(topUp);

        entries.Should().OnlyContain(x => x.BalanceType == LedgerBalanceType.Regular);
        entries.Should().OnlyContain(x => x.ReferenceType == LedgerReferenceType.TopUp);
        entries.Should().OnlyContain(x => x.ReferenceId == topUp.Id.ToString());
        wallet.GiftBalance.Should().Be(0m);
        wallet.WithdrawableBalance.Should().Be(topUp.AmountNoor);
    }

    [Fact]
    public async Task SettleAsync_WhenUnifiedCreditFails_DoesNotSave()
    {
        var topUp = TopUpRequest.CreateAdminCardToCard(UserId.New(), UserId.New(), 10_000m, 10_000m, null, "key");
        var unitOfWork = new Mock<IUnitOfWork>();
        var noorCredit = new Mock<INoorCreditService>();
        noorCredit.Setup(x => x.CreditAsync(It.IsAny<NoorCreditRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("اعتبارسازی ناموفق"));
        var service = new TopUpSettlementService(noorCredit.Object, unitOfWork.Object);

        var act = async () => await service.SettleAsync(topUp);

        await act.Should().ThrowAsync<DomainException>();
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
