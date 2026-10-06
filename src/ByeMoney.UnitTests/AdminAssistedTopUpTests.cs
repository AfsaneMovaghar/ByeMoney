using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Resources;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using MediatR;
using Xunit;

namespace ByeMoney.UnitTests;

public class AdminAssistedTopUpTests
{
    [Fact]
    public async Task Handle_CreatesConfirmedSnapshotAndSettlesOnce()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>())).ReturnsAsync(beneficiary);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10_000m);
        var topUps = new Mock<ITopUpRequestRepository>();
        TopUpRequest? created = null;
        topUps.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TopUpRequest, CancellationToken>((topUp, _) => created = topUp).Returns(Task.CompletedTask);
        var settlement = new Mock<ITopUpSettlementService>();
        settlement.Setup(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new AdminAssistedTopUpService(users.Object, settings.Object, topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), Mock.Of<ISender>());

        var actorId = Guid.NewGuid();
        var result = await service.CreateAsync(new("external-user", actorId, 25_000m, "receipt-id", "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        created.Should().NotBeNull();
        created!.Status.Should().Be(TopUpStatus.Confirmed);
        created.AmountNoor.Should().Be(25m);
        created.AmountRial.Should().Be(250_000m);
        created.RialPerNoorSnapshot.Should().Be(10_000m);
        created.ChargeType.Should().Be(ChargeType.AdminAssistedCardToCard);
        created.UserId.Should().Be(beneficiary.Id);
        created.CreatedByUserId.Should().Be(new UserId(actorId));
        settlement.Verify(x => x.SettleAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsExistingTopUpOnIdempotentRetryWithoutSettlingAgain()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var actorId = UserId.New();
        var existing = TopUpRequest.CreateAdminCardToCard(beneficiary.Id, actorId, 10_000m, 10_000m, "receipt-id", "idem-1");
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>())).ReturnsAsync(beneficiary);
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), Mock.Of<ISender>());

        var result = await service.CreateAsync(new("external-user", actorId.Value, 10_000m, "receipt-id", "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpRequestId.Should().Be(existing.Id.Value);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        topUps.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsDifferentReceiptForTheSameIdempotencyKey()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var actorId = UserId.New();
        var existing = TopUpRequest.CreateAdminCardToCard(beneficiary.Id, actorId, 10_000m, 10_000m, "first-receipt", "idem-1");
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>())).ReturnsAsync(beneficiary);
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), Mock.Of<ISender>());

        var result = await service.CreateAsync(new("external-user", actorId.Value, 10_000m, "second-receipt", "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.Status.Should().Be(ByeMoney.Domain.Common.ResultStatus.Conflict);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SyncsMissingBeneficiaryBeforeSettling()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var users = new Mock<IUserRepository>();
        users.SetupSequence(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null)
            .ReturnsAsync(beneficiary);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<SyncUserFromStrapiCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(beneficiary.Id.Value);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10_000m);
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, settings.Object, topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), sender.Object);

        var result = await service.CreateAsync(new("external-user", Guid.NewGuid(), 100_000m, "receipt-id", "idem-2", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.Verify(x => x.Send(It.Is<SyncUserFromStrapiCommand>(c => c.ExternalUserId == "external-user"), It.IsAny<CancellationToken>()), Times.Once);
        settlement.Verify(x => x.SettleAsync(It.Is<TopUpRequest>(t => t.UserId == beneficiary.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotSettleWhenSynchronizedBeneficiaryIsInactive()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: false);
        var users = new Mock<IUserRepository>();
        users.SetupSequence(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null)
            .ReturnsAsync(beneficiary);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<SyncUserFromStrapiCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(beneficiary.Id.Value);
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), sender.Object);

        var result = await service.CreateAsync(new("external-user", Guid.NewGuid(), 100_000m, "receipt-id", "idem-3", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        topUps.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotSettleWhenBeneficiaryIsMissingInTarhElahi()
    {
        var users = new Mock<IUserRepository>();
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<SyncUserFromStrapiCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(string.Format(ApplicationErrors.User_NotFoundInTarhElahi, "unknown-user")));
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), sender.Object);

        var result = await service.CreateAsync(new("unknown-user", Guid.NewGuid(), 100_000m, "receipt-id", "idem-4", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.Status.Should().Be(ByeMoney.Domain.Common.ResultStatus.NotFound);
        topUps.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUnreferencedReceipt_RetainsReceiptWhenAlreadyReferenced()
    {
        var topUps = new Mock<ITopUpRequestRepository>();
        var existing = TopUpRequest.CreateAdminCardToCard(UserId.New(), UserId.New(), 100_000m, 10_000m, "receipt-id", "same-key");
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("same-key", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var receipts = new Mock<IReceiptStorage>();
        var service = new AdminAssistedTopUpService(Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object, Mock.Of<ITopUpSettlementService>(), receipts.Object, Mock.Of<ISender>());

        await service.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", CancellationToken.None);

        receipts.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUnreferencedReceipt_DeletesReceiptWhenNotReferenced()
    {
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("same-key", It.IsAny<CancellationToken>())).ReturnsAsync((TopUpRequest?)null);
        var receipts = new Mock<IReceiptStorage>();
        var service = new AdminAssistedTopUpService(Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object, Mock.Of<ITopUpSettlementService>(), receipts.Object, Mock.Of<ISender>());

        await service.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", CancellationToken.None);

        receipts.Verify(x => x.DeleteAsync("receipt-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_SkipsReceiptLookupWhenReceiptIdIsMissing()
    {
        var receipts = new Mock<IReceiptStorage>();
        var validator = new CreateAdminCardToCardTopUpCommandValidator(receipts.Object);

        var result = await validator.ValidateAsync(new CreateAdminCardToCardTopUpCommand(
            "external-user", Guid.NewGuid(), 100_000m, "", "idem-5", null,
            ChargeType.AdminAssistedCardToCard));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.ErrorMessage == ApplicationErrors.TopUpRequest_ReceiptRequired);
        receipts.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
