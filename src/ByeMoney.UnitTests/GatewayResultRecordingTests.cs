using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class GatewayResultRecordingTests
{
    private static (GatewayTopUpService Service, TopUpRequest TopUp,
        Mock<ITopUpSettlementService> Settlement, Mock<IUnitOfWork> UnitOfWork) Create()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 1000m);
        var repo = new Mock<ITopUpRequestRepository>();
        repo.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);
        var settlement = new Mock<ITopUpSettlementService>();
        settlement.Setup(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (new GatewayTopUpService(repo.Object, settlement.Object, unitOfWork.Object,
            Mock.Of<IPublisher>(), Mock.Of<IServiceScopeFactory>()), topUp, settlement, unitOfWork);
    }

    private static RecordGatewayResultCommand Command(TopUpRequest topUp, string kind, string eventId,
        decimal original = 10_000m, decimal affective = 10_000m) =>
        new(topUp.ClientReferenceCode, "SEP", eventId, kind,
            kind == GatewayResultKinds.Unpaid ? null : "bank-ref",
            kind == GatewayResultKinds.Verified ? "bank-rrn" : null,
            "0", kind == GatewayResultKinds.Verified ? original : null,
            kind == GatewayResultKinds.Verified ? affective : null, DateTime.UtcNow);

    [Fact]
    public async Task DuplicateVerifiedOutcomeAndLostResponseSettleOnlyOnce()
    {
        var (service, topUp, settlement, _) = Create();
        var command = Command(topUp, GatewayResultKinds.Verified, "verify-1");

        (await service.RecordResultAsync(command)).IsSuccess.Should().BeTrue();
        (await service.RecordResultAsync(command)).IsSuccess.Should().BeTrue();

        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        settlement.Verify(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConflictingRepeatOfSameBankEventIsRejected()
    {
        var (service, topUp, settlement, _) = Create();
        var first = Command(topUp, GatewayResultKinds.Verified, "verify-1");
        (await service.RecordResultAsync(first)).IsSuccess.Should().BeTrue();

        var conflict = await service.RecordResultAsync(first with { AffectiveAmountRial = 9999m });

        conflict.IsFailure.Should().BeTrue();
        conflict.ErrorCode.Should().Be(GatewayTopUpErrorCodes.GatewayResultConflict);
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        settlement.Verify(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MismatchedVerifiedAmountKeepsTopUpPendingWithoutNoor()
    {
        var (service, topUp, settlement, _) = Create();

        var result = await service.RecordResultAsync(Command(topUp, GatewayResultKinds.Verified,
            "verify-mismatch", original: 9999m));

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpAmountMismatch);
        topUp.Status.Should().Be(TopUpStatus.Pending);
        topUp.ExternalTransactionId.Should().Be("bank-ref");
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedReverseNeedsReviewUntilSuccessIsRecorded()
    {
        var (service, topUp, settlement, _) = Create();
        await service.RecordResultAsync(Command(topUp, GatewayResultKinds.ReverseFailed, "reverse-failed"));
        topUp.Status.Should().Be(TopUpStatus.Pending);

        (await service.RecordResultAsync(Command(topUp, GatewayResultKinds.ReverseSucceeded,
            "reverse-success"))).IsSuccess.Should().BeTrue();

        topUp.Status.Should().Be(TopUpStatus.Rejected);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnpaidOutcomeCannotRejectAlreadyConfirmedTopUp()
    {
        var (service, topUp, settlement, _) = Create();
        await service.RecordResultAsync(Command(topUp, GatewayResultKinds.Verified, "verify-1"));

        var result = await service.RecordResultAsync(Command(topUp, GatewayResultKinds.Unpaid, "unpaid-2"));

        result.IsFailure.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        settlement.Verify(x => x.SettleAsync(topUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifiedOutcomeCannotConfirmRejectedTopUp()
    {
        var (service, topUp, settlement, _) = Create();
        await service.RecordResultAsync(Command(topUp, GatewayResultKinds.Unpaid, "unpaid-1"));

        var result = await service.RecordResultAsync(Command(topUp, GatewayResultKinds.Verified, "verify-2"));

        result.IsFailure.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Rejected);
        settlement.Verify(x => x.SettleAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GatewayResultPreservesOccurredAtUtc()
    {
        var (service, topUp, _, _) = Create();
        var occurredAt = new DateTime(2026, 10, 3, 14, 30, 0, DateTimeKind.Utc);
        var command = Command(topUp, GatewayResultKinds.Verified, "verify-time") with
        {
            OccurredAtUtc = occurredAt
        };

        var result = await service.RecordResultAsync(command);

        result.IsSuccess.Should().BeTrue();
        topUp.GatewayResultAtUtc.Should().Be(occurredAt);
    }
}
