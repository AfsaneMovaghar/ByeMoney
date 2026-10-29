using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Resources;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class WelcomeGiftServiceTests
{
    [Fact]
    public void GiftCampaignSetting_Create_ThrowsDomainException_WhenAmountIsNegative()
    {
        var act = () => GiftCampaignSetting.Create(GiftCampaignType.Welcome, -10m, true);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.GiftCampaign_AmountCannotBeNegative);
    }

    [Fact]
    public void GiftCampaignSetting_Create_ThrowsDomainException_WhenStartDateIsAfterEndDate()
    {
        var now = DateTime.UtcNow;
        var act = () => GiftCampaignSetting.Create(
            GiftCampaignType.Welcome,
            50m,
            true,
            startDateUtc: now.AddDays(2),
            endDateUtc: now.AddDays(1));

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.GiftCampaign_StartDateCannotBeAfterEndDate);
    }

    [Fact]
    public void GiftGrantBatch_Create_SetsCreatedAtUtc_WhenSpecified()
    {
        var customDate = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var batch = GiftGrantBatch.Create(
            "key-1",
            "hash-1",
            GiftGrantSource.WelcomeBonus,
            "Reason",
            50m,
            null,
            createdAtUtc: customDate);

        batch.CreatedAtUtc.Should().Be(customDate);
    }

    [Fact]
    public async Task NoorCreditService_CreditAsync_ThrowsDomainException_WhenAmountIsNotPositive()
    {
        var provisioningMock = new Mock<IUserWalletProvisioningService>();
        var postingMock = new Mock<IWalletPostingService>();
        var service = new NoorCreditService(provisioningMock.Object, postingMock.Object);

        var request = new NoorCreditRequest(
            new UserId(Guid.NewGuid()),
            AmountNoor: 0m,
            BalanceType: LedgerBalanceType.Gift,
            ReferenceType: LedgerReferenceType.WelcomeGift);

        var act = async () => await service.CreditAsync(request);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage(DomainErrors.Wallet_CreditAmountMustBePositive);
    }

    [Fact]
    public async Task NoorCreditService_CreditAsync_ReturnsCorrectNewWithdrawableBalance()
    {
        var provisioningMock = new Mock<IUserWalletProvisioningService>();
        var postingMock = new Mock<IWalletPostingService>();

        var userId = new UserId(Guid.NewGuid());
        var wallet = Wallet.Create(Account.CreateUserAccount(userId).Id, userId);
        var account = Account.CreateUserAccount(userId);
        var sysAccount = Account.CreateSystemAccount();

        provisioningMock.Setup(p => p.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(account, wallet));
        provisioningMock.Setup(p => p.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sysAccount);

        var service = new NoorCreditService(provisioningMock.Object, postingMock.Object);

        var request = new NoorCreditRequest(
            userId,
            AmountNoor: 100m,
            BalanceType: LedgerBalanceType.Gift,
            ReferenceType: LedgerReferenceType.WelcomeGift,
            ReferenceId: "batch-1",
            Reason: "Test");

        var result = await service.CreditAsync(request);

        result.AmountCredited.Should().Be(100m);
        result.NewWithdrawableBalance.Should().Be(0m);
        result.NewGiftBalance.Should().Be(0m);
    }

    [Fact]
    public async Task WelcomeGiftService_ComputesRealPayloadHash_AndUsesPersianReason()
    {
        var giftSettingRepoMock = new Mock<IGiftCampaignSettingRepository>();
        var giftGrantRepoMock = new Mock<IGiftGrantRepository>();
        var noorCreditMock = new Mock<INoorCreditService>();
        var provisioningMock = new Mock<IUserWalletProvisioningService>();

        var activeSetting = GiftCampaignSetting.Create(GiftCampaignType.Welcome, 50m, isEnabled: true);
        giftSettingRepoMock.Setup(x => x.GetByTypeAsync(GiftCampaignType.Welcome, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSetting);

        GiftGrantBatch? capturedBatch = null;
        giftGrantRepoMock.Setup(x => x.AddBatchAsync(It.IsAny<GiftGrantBatch>(), It.IsAny<CancellationToken>()))
            .Callback<GiftGrantBatch, CancellationToken>((batch, _) => capturedBatch = batch)
            .Returns(Task.CompletedTask);

        noorCreditMock.Setup(x => x.CreditAsync(It.IsAny<NoorCreditRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoorCreditResult(Guid.NewGuid(), 50m, 0m, 50m, null!));

        var user = User.CreateFromStrapi("ext-user-123");
        var service = new WelcomeGiftService(
            giftSettingRepoMock.Object,
            giftGrantRepoMock.Object,
            noorCreditMock.Object,
            provisioningMock.Object);

        await service.GrantWelcomeGiftIfEligibleAsync(user);

        capturedBatch.Should().NotBeNull();
        capturedBatch!.Reason.Should().Be(ApplicationErrors.Gift_WelcomeBonusReason);
        capturedBatch.RequestPayloadHash.Should().HaveLength(64);
        capturedBatch.RequestPayloadHash.Should().NotBe(capturedBatch.IdempotencyKey);
    }
}

