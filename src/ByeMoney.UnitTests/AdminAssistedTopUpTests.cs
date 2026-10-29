using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public class AdminAssistedTopUpTests
{
    private static Mock<IUserProvisioningService> ProvisionedBeneficiary(User beneficiary, bool created = false)
    {
        var provisioning = new Mock<IUserProvisioningService>();
        provisioning
            .Setup(x => x.EnsureUserFromExternalAsync(beneficiary.ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(beneficiary, created));

        return provisioning;
    }
    [Theory]
    [InlineData(null)]
    [InlineData("receipt-id")]
    public async Task Handle_CreatesConfirmedSnapshotAndSettlesOnce(string? receiptId)
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
        var service = new AdminAssistedTopUpService(users.Object, settings.Object, topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), ProvisionedBeneficiary(beneficiary).Object);

        var actorId = Guid.NewGuid();
        var result = await service.CreateAsync(new("external-user", actorId, 25_000m, receiptId, "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        created.Should().NotBeNull();
        created!.Status.Should().Be(TopUpStatus.Confirmed);
        created.ReceiptId.Should().Be(receiptId);
        created.AmountNoor.Should().Be(25m);
        created.AmountRial.Should().Be(250_000m);
        created.RialPerNoorSnapshot.Should().Be(10_000m);
        created.ChargeType.Should().Be(ChargeType.AdminAssistedCardToCard);
        created.UserId.Should().Be(beneficiary.Id);
        created.CreatedByUserId.Should().Be(new UserId(actorId));
        settlement.Verify(x => x.SettleAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("receipt-id")]
    public async Task Handle_ReturnsExistingTopUpOnIdempotentRetryWithoutSettlingAgain(string? receiptId)
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var actorId = UserId.New();
        var existing = TopUpRequest.CreateAdminCardToCard(beneficiary.Id, actorId, 10_000m, 10_000m, receiptId, "idem-1");
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>())).ReturnsAsync(beneficiary);
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), ProvisionedBeneficiary(beneficiary).Object);

        var result = await service.CreateAsync(new("external-user", actorId.Value, 10_000m, receiptId, "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpRequestId.Should().Be(existing.Id.Value);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        topUps.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("first-receipt", "second-receipt")]
    [InlineData(null, "receipt-id")]
    [InlineData("receipt-id", null)]
    public async Task Handle_RejectsDifferentReceiptForTheSameIdempotencyKey(string? originalReceiptId, string? retryReceiptId)
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var actorId = UserId.New();
        var existing = TopUpRequest.CreateAdminCardToCard(beneficiary.Id, actorId, 10_000m, 10_000m, originalReceiptId, "idem-1");
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByExternalUserIdAsync("external-user", It.IsAny<CancellationToken>())).ReturnsAsync(beneficiary);
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(users.Object, Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object, Mock.Of<IReceiptStorage>(), ProvisionedBeneficiary(beneficiary).Object);

        var result = await service.CreateAsync(new("external-user", actorId.Value, 10_000m, retryReceiptId, "idem-1", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.Status.Should().Be(ByeMoney.Domain.Common.ResultStatus.Conflict);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProvisionsMissingBeneficiaryBeforeSettling()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: true);
        var provisioning = ProvisionedBeneficiary(beneficiary, created: true);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10_000m);
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(
            Mock.Of<IUserRepository>(), settings.Object, topUps.Object, settlement.Object,
            Mock.Of<IReceiptStorage>(), provisioning.Object);

        var result = await service.CreateAsync(new("external-user", Guid.NewGuid(), 100_000m, "receipt-id", "idem-2", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        provisioning.Verify(
            x => x.EnsureUserFromExternalAsync("external-user", It.IsAny<CancellationToken>()),
            Times.Once);
        settlement.Verify(x => x.SettleAsync(It.Is<TopUpRequest>(t => t.UserId == beneficiary.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotSettleWhenSynchronizedBeneficiaryIsInactive()
    {
        var beneficiary = User.CreateFromStrapi("external-user", confirmed: false);
        var provisioning = ProvisionedBeneficiary(beneficiary, created: true);
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(
            Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object,
            Mock.Of<IReceiptStorage>(), provisioning.Object);

        var result = await service.CreateAsync(new("external-user", Guid.NewGuid(), 100_000m, "receipt-id", "idem-3", null,
            ChargeType.AdminAssistedCardToCard), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        topUps.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotSettleWhenBeneficiaryIsMissingInTarhElahi()
    {
        var provisioning = new Mock<IUserProvisioningService>();
        provisioning.Setup(x => x.EnsureUserFromExternalAsync("unknown-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(string.Format(ApplicationErrors.User_NotFoundInTarhElahi, "unknown-user")));
        var topUps = new Mock<ITopUpRequestRepository>();
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new AdminAssistedTopUpService(
            Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object, settlement.Object,
            Mock.Of<IReceiptStorage>(), provisioning.Object);

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
        var service = new AdminAssistedTopUpService(
            Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object,
            Mock.Of<ITopUpSettlementService>(), receipts.Object, Mock.Of<IUserProvisioningService>());

        await service.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", CancellationToken.None);

        receipts.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUnreferencedReceipt_DeletesReceiptWhenNotReferenced()
    {
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdempotencyKeyAsync("same-key", It.IsAny<CancellationToken>())).ReturnsAsync((TopUpRequest?)null);
        var receipts = new Mock<IReceiptStorage>();
        var service = new AdminAssistedTopUpService(
            Mock.Of<IUserRepository>(), Mock.Of<ISystemSettingRepository>(), topUps.Object,
            Mock.Of<ITopUpSettlementService>(), receipts.Object, Mock.Of<IUserProvisioningService>());

        await service.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", CancellationToken.None);

        receipts.Verify(x => x.DeleteAsync("receipt-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_SkipsReceiptLookupWhenReceiptIdIsMissing()
    {
        var receipts = new Mock<IReceiptStorage>();
        var validator = new CreateAdminCardToCardTopUpCommandValidator(receipts.Object);

        var result = await validator.ValidateAsync(new CreateAdminCardToCardTopUpCommand(
            "external-user", Guid.NewGuid(), 100_000m, null, "idem-5", null,
            ChargeType.AdminAssistedCardToCard));

        result.IsValid.Should().BeTrue();
        receipts.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Create_WithoutReceiptStillRequiresIdempotencyKey()
    {
        var create = () => TopUpRequest.CreateAdminCardToCard(
            UserId.New(), UserId.New(), 10_000m, 10_000m, null, " ");

        create.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-receipt")]
    public async Task Validator_RejectsInvalidProvidedReceipt(string receiptId)
    {
        var receipts = new Mock<IReceiptStorage>();
        var validator = new CreateAdminCardToCardTopUpCommandValidator(receipts.Object);
        var result = await validator.ValidateAsync(new CreateAdminCardToCardTopUpCommand(
            "external-user", Guid.NewGuid(), 100_000m, receiptId, "idem-5", null,
            ChargeType.AdminAssistedCardToCard));

        result.IsValid.Should().BeFalse();
    }
}
