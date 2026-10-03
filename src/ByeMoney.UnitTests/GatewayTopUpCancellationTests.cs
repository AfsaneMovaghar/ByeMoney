using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ByeMoney.UnitTests;

public class GatewayTopUpCancellationTests
{
    private static (CancelGatewayTopUpCommandHandler Handler, TopUpRequest TopUp,
        Mock<ITopUpRequestRepository> Repository, Mock<IUnitOfWork> UnitOfWork) CreateHandler()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);
        var unitOfWork = new Mock<IUnitOfWork>();
        return (new CancelGatewayTopUpCommandHandler(repository.Object, unitOfWork.Object),
            topUp, repository, unitOfWork);
    }

    [Fact]
    public async Task CancellationRejectsPendingTopUpWithoutFinancialSettlement()
    {
        var (handler, topUp, repository, unitOfWork) = CreateHandler();

        var result = await handler.Handle(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Rejected);
        topUp.RejectedAtUtc.Should().NotBeNull();
        topUp.RejectionReason.Should().NotBeNullOrWhiteSpace();
        topUp.GatewayName.Should().Be("SEP");
        repository.Verify(x => x.Update(topUp), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepeatedCancellationDoesNotWriteAgain()
    {
        var (handler, topUp, repository, unitOfWork) = CreateHandler();
        var command = new CancelGatewayTopUpCommand(topUp.ClientReferenceCode);

        (await handler.Handle(command, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(command, CancellationToken.None)).IsSuccess.Should().BeTrue();

        repository.Verify(x => x.Update(topUp), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationCannotUndoConfirmedPayment()
    {
        var (handler, topUp, repository, unitOfWork) = CreateHandler();
        topUp.ConfirmGateway("bank-ref", "bank-rrn", "SEP");

        var result = await handler.Handle(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpAlreadyConfirmed);
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        repository.Verify(x => x.Update(It.IsAny<TopUpRequest>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingReviewRejectionIsNotReportedAsGatewayCancellation()
    {
        var (handler, topUp, repository, unitOfWork) = CreateHandler();
        topUp.Reject("رد پس از بررسی مالی");

        var result = await handler.Handle(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode),
            CancellationToken.None);

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpRequiresReview);
        repository.Verify(x => x.Update(It.IsAny<TopUpRequest>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellationCannotRejectNonGatewayTopUp()
    {
        var (handler, topUp, repository, unitOfWork) = CreateHandler();
        var cardTopUp = TopUpRequest.Create(UserId.New(), 10m, PaymentMethod.CardToCard,
            clientReferenceCode: topUp.ClientReferenceCode);
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardTopUp);

        var result = await handler.Handle(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode),
            CancellationToken.None);

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.InvalidVerificationData);
        cardTopUp.Status.Should().Be(TopUpStatus.Pending);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ControllerTranslatesCancellationAndMismatchWithTypedResponses()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation.ReportGatewayCancellationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        sender.Setup(x => x.Send(It.IsAny<ConfirmGatewayTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayConfirmationOutcome(Result.Failure("مغایرت مبلغ",
                GatewayTopUpErrorCodes.TopUpAmountMismatch)));
        var controller = new GatewayTopUpController(sender.Object,
            NullLogger<GatewayTopUpController>.Instance);

        var cancelled = await controller.Cancel(new GatewayCancellationRequest("TR-123", "SEP"),
            CancellationToken.None);
        var mismatch = await controller.Confirm(new GatewayConfirmationRequest(
            "TR-123", "SEP", "tx-1", "rrn-1", 100m, 99m), CancellationToken.None);

        cancelled.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<GatewayCancellationResponse>();
        mismatch.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.Value.Should().BeOfType<GatewayErrorResponse>()
            .Which.Code.Should().Be(GatewayTopUpErrorCodes.TopUpAmountMismatch);
    }
}
