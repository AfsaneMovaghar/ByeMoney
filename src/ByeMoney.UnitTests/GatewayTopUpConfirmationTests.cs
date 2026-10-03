using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Settings;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class GatewayTopUpConfirmationTests
{
    private static TopUpRequest CreateTopUp() => TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);

    private static ConfirmTopUpCommand Command(TopUpRequest topUp, decimal original = 100_000m,
        decimal affective = 100_000m, string refNum = "bank-ref")
        => new(new TopUpRequestId(Guid.Empty), refNum, 0m, topUp.ClientReferenceCode,
            "SEP", "bank-rrn", original, affective);

    private static (ConfirmTopUpCommandHandler Handler, Mock<ITopUpSettlementService> Settlement,
        Mock<ITopUpRequestRepository> Repository) CreateHandler(TopUpRequest topUp)
    {
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);
        var settlement = new Mock<ITopUpSettlementService>();
        settlement.Setup(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (new ConfirmTopUpCommandHandler(repository.Object, settlement.Object, Mock.Of<IPublisher>()),
            settlement, repository);
    }

    [Theory]
    [InlineData(99_999, 100_000)]
    [InlineData(100_000, 99_999)]
    public async Task VerifiedAmountMismatchDoesNotSettle(decimal original, decimal affective)
    {
        var topUp = CreateTopUp();
        var (handler, settlement, _) = CreateHandler(topUp);

        var result = await handler.Handle(Command(topUp, original, affective), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpAmountMismatch);
        topUp.Status.Should().Be(TopUpStatus.Pending);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SameRefNumAndTopUpIsIdempotent()
    {
        var topUp = CreateTopUp();
        var (handler, settlement, _) = CreateHandler(topUp);

        (await handler.Handle(Command(topUp), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(Command(topUp), CancellationToken.None)).IsSuccess.Should().BeTrue();

        settlement.Verify(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SameRefNumOnAnotherTopUpConflicts()
    {
        var topUp = CreateTopUp();
        var other = CreateTopUp();
        var (handler, settlement, repository) = CreateHandler(topUp);
        repository.Setup(x => x.GetByExternalTransactionIdAsync("bank-ref", It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        var result = await handler.Handle(Command(topUp), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.RefNumConflict);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectedTopUpRequiresReviewWithoutSettlement()
    {
        var topUp = CreateTopUp();
        topUp.Reject("رد توسط ادمین");
        var (handler, settlement, _) = CreateHandler(topUp);

        var result = await handler.Handle(Command(topUp), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpRequiresReview);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminConfirmationCannotBypassGatewayVerification()
    {
        var topUp = CreateTopUp();
        var (handler, settlement, repository) = CreateHandler(topUp);
        repository.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);

        var result = await handler.Handle(new ConfirmTopUpCommand(topUp.Id, "bank-ref", topUp.Amount),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConcurrentCallbacksOnSameTrackedTopUpSettleOnce()
    {
        var topUp = CreateTopUp();
        var (handler, settlement, _) = CreateHandler(topUp);

        var results = await Task.WhenAll(
            handler.Handle(Command(topUp), CancellationToken.None),
            handler.Handle(Command(topUp), CancellationToken.None));

        results.Should().OnlyContain(x => x.IsSuccess);
        settlement.Verify(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidatorRejectsMissingBankData()
    {
        var topUp = CreateTopUp();
        var validator = new ConfirmTopUpCommandValidator();
        var command = Command(topUp) with { BankReferenceNumber = "", OriginalAmountRial = null };

        var validation = await validator.ValidateAsync(command);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(x => x.PropertyName).Should().Contain(nameof(command.BankReferenceNumber))
            .And.Contain(nameof(command.OriginalAmountRial));
    }

    [Fact]
    public async Task GatewayCreationFreezesWholeNoorRateAndRial()
    {
        var repository = new Mock<ITopUpRequestRepository>();
        TopUpRequest? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TopUpRequest, CancellationToken>((topUp, _) => saved = topUp)
            .Returns(Task.CompletedTask);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10_000m);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, unitOfWork.Object, settings.Object);

        var result = await handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 10m, PaymentMethod.Gateway),
            CancellationToken.None);

        result.AmountRial.Should().Be(100_000m);
        saved!.Amount.Should().Be(10m);
        saved.AmountRial.Should().Be(100_000m);
        saved.RialPerNoorSnapshot.Should().Be(10_000m);
        saved.PaymentMethod.Should().Be(PaymentMethod.Gateway);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public async Task GatewayCreationRejectsInvalidRate(decimal rate)
    {
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);
        var repository = new Mock<ITopUpRequestRepository>();
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), settings.Object);

        await FluentActions.Invoking(() => handler.Handle(
            new CreateTopUpRequestCommand(Guid.NewGuid(), 10m, PaymentMethod.Gateway), CancellationToken.None))
            .Should().ThrowAsync<ByeMoney.Domain.Common.Exceptions.DomainException>();
        repository.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GatewayCreateValidatorRequiresWholeNoor()
    {
        var validator = new CreateTopUpRequestCommandValidator();
        var result = await validator.ValidateAsync(
            new CreateTopUpRequestCommand(Guid.NewGuid(), 1.5m, PaymentMethod.Gateway));

        result.IsValid.Should().BeFalse();
    }
}
