using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ByeMoney.UnitTests;

public class GatewayTopUpCancellationTests
{
    private static (GatewayTopUpService Service, TopUpRequest TopUp,
        Mock<ITopUpRequestRepository> Repository, Mock<IUnitOfWork> UnitOfWork) CreateService()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);
        var unitOfWork = new Mock<IUnitOfWork>();
        var settlement = new Mock<ITopUpSettlementService>();
        var publisher = new Mock<IPublisher>();
        var scopes = new Mock<IServiceScopeFactory>();
        return (new GatewayTopUpService(repository.Object, settlement.Object, unitOfWork.Object, publisher.Object, scopes.Object),
            topUp, repository, unitOfWork);
    }

    [Fact]
    public async Task CancellationRejectsPendingTopUpWithoutFinancialSettlement()
    {
        var (service, topUp, repository, unitOfWork) = CreateService();

        var result = await service.CancelAsync(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP"),
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
        var (service, topUp, repository, unitOfWork) = CreateService();
        var command = new CancelGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP");

        (await service.CancelAsync(command, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await service.CancelAsync(command, CancellationToken.None)).IsSuccess.Should().BeTrue();

        repository.Verify(x => x.Update(topUp), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationCannotUndoConfirmedPayment()
    {
        var (service, topUp, repository, unitOfWork) = CreateService();
        topUp.ConfirmGateway("bank-ref", "bank-rrn", "SEP");

        var result = await service.CancelAsync(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP"),
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
        var (service, topUp, repository, unitOfWork) = CreateService();
        topUp.Reject("رد پس از بررسی مالی");

        var result = await service.CancelAsync(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP"),
            CancellationToken.None);

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpRequiresReview);
        repository.Verify(x => x.Update(It.IsAny<TopUpRequest>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellationCannotRejectNonGatewayTopUp()
    {
        var (service, topUp, repository, unitOfWork) = CreateService();
        var cardTopUp = TopUpRequest.Create(UserId.New(), 10m, PaymentMethod.CardToCard,
            clientReferenceCode: topUp.ClientReferenceCode);
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardTopUp);

        var result = await service.CancelAsync(new CancelGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP"),
            CancellationToken.None);

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.InvalidVerificationData);
        cardTopUp.Status.Should().Be(TopUpStatus.Pending);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ControllerTranslatesCancellationAndMismatchWithTypedResponses()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelGatewayTopUpCommand>(), It.IsAny<CancellationToken>()))
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

