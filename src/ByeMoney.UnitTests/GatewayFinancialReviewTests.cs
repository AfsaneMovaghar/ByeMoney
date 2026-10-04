using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class GatewayFinancialReviewTests
{
    private static TopUpRequest NewTopUp(string? reference = null) => TopUpRequest.Create(
        UserId.New(), 10m, PaymentMethod.Gateway, clientReferenceCode: reference, rialPerNoor: 1000m);

    [Fact]
    public void DuplicateOpenIsIdempotent()
    {
        var topUp = NewTopUp();
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.NoCallback).IsSuccess.Should().BeTrue();
        var opened = topUp.ReviewOpenedAtUtc;

        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.NoCallback).IsSuccess.Should().BeTrue();

        topUp.ReviewOpenedAtUtc.Should().Be(opened);
        topUp.Status.Should().Be(TopUpStatus.Pending);
    }

    [Fact]
    public void ConflictingOpenIsRejected()
    {
        var topUp = NewTopUp();
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.NoCallback);

        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.VerifyUnknown).IsFailure.Should().BeTrue();
        topUp.OpenFinancialReview("case-2", FinancialReviewReasonCodes.NoCallback).IsFailure.Should().BeTrue();
        topUp.ReviewReasonCode.Should().Be(FinancialReviewReasonCodes.NoCallback);
    }

    [Fact]
    public void OpenAfterConfirmedKeepsConfirmedState()
    {
        var topUp = NewTopUp();
        topUp.ConfirmGateway("bank-ref", "bank-rrn", "SEP").IsSuccess.Should().BeTrue();

        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.DeliveryUnknown).IsSuccess.Should().BeTrue();

        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        topUp.ExternalTransactionId.Should().Be("bank-ref");
    }

    [Fact]
    public void OpenAfterRejectedKeepsRejectedState()
    {
        var topUp = NewTopUp();
        topUp.Reject("Bank unpaid");

        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.NoCallback).IsSuccess.Should().BeTrue();

        topUp.Status.Should().Be(TopUpStatus.Rejected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentOpenVersusTerminalTransitionReloadsFinalState(bool confirm)
    {
        var stale = NewTopUp();
        var fresh = NewTopUp(stale.ClientReferenceCode);
        if (confirm) fresh.ConfirmGateway("bank-ref", "bank-rrn", "SEP");
        else fresh.Reject("Bank unpaid");
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(stale.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        var freshRepository = new Mock<ITopUpRequestRepository>();
        freshRepository.Setup(x => x.GetByClientReferenceCodeAsync(stale.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fresh);
        var work = new Mock<IUnitOfWork>();
        work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var freshWork = new Mock<IUnitOfWork>();
        freshWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(ITopUpRequestRepository))).Returns(freshRepository.Object);
        provider.Setup(x => x.GetService(typeof(IUnitOfWork))).Returns(freshWork.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(x => x.CreateScope()).Returns(scope.Object);
        var service = new GatewayTopUpService(repository.Object, Mock.Of<ITopUpSettlementService>(),
            work.Object, Mock.Of<IPublisher>(), scopes.Object);

        var result = await service.OpenReviewAsync(stale.ClientReferenceCode, "case-1",
            FinancialReviewReasonCodes.VerifyUnknown);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopUpStatus.Should().Be(confirm ? nameof(TopUpStatus.Confirmed) : nameof(TopUpStatus.Rejected));
        fresh.ReviewCaseId.Should().Be("case-1");
        freshWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResolveRequiresMatchingTerminalState()
    {
        var topUp = NewTopUp();
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.VerifyUnknown);

        topUp.ResolveFinancialReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed,
            "bank-ref").IsFailure.Should().BeTrue();
        topUp.ReviewResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void VerifiedAndConfirmedCanResolveOnlyOnceWithSameContent()
    {
        var topUp = NewTopUp();
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.VerifyUnknown);
        topUp.RecordGatewayResult("event-1", "SEP", GatewayResultKinds.Verified,
            "bank-ref", "bank-rrn", "0", 10_000m, 10_000m, DateTime.UtcNow);
        topUp.ConfirmGateway("bank-ref", "bank-rrn", "SEP");

        topUp.ResolveFinancialReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed,
            "bank-ref").IsSuccess.Should().BeTrue();
        var resolvedAt = topUp.ReviewResolvedAtUtc;
        topUp.ResolveFinancialReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed,
            "bank-ref").IsSuccess.Should().BeTrue();
        topUp.ResolveFinancialReview("case-1", FinancialReviewOutcomeCodes.ReversedRejected,
            "bank-ref").IsFailure.Should().BeTrue();
        topUp.ReviewResolvedAtUtc.Should().Be(resolvedAt);
    }

    [Theory]
    [InlineData(GatewayResultKinds.Unpaid, FinancialReviewOutcomeCodes.UnpaidRejected, null)]
    [InlineData(GatewayResultKinds.ReverseSucceeded, FinancialReviewOutcomeCodes.ReversedRejected, "bank-ref")]
    public void RejectedGatewayResultCanResolveMatchingCase(string kind, string outcome, string? reference)
    {
        var topUp = NewTopUp();
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.BankConflict);
        topUp.RecordGatewayResult("event-1", "SEP", kind, reference, null, "0", null, null, DateTime.UtcNow);
        topUp.Reject("Bank result");

        topUp.ResolveFinancialReview("case-1", outcome, reference).IsSuccess.Should().BeTrue();
        topUp.ReviewOutcomeCode.Should().Be(outcome);
    }

    [Fact]
    public async Task ConfirmedThenManualRefundIsExplicitlyUnsupported()
    {
        var topUp = NewTopUp();
        topUp.ConfirmGateway("bank-ref", "bank-rrn", "SEP");
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.BankConflict);
        var service = new GatewayTopUpService(Mock.Of<ITopUpRequestRepository>(),
            Mock.Of<ITopUpSettlementService>(), Mock.Of<IUnitOfWork>(), Mock.Of<IPublisher>(),
            Mock.Of<IServiceScopeFactory>());

        var result = await service.ResolveReviewAsync(topUp.ClientReferenceCode, "case-1",
            FinancialReviewOutcomeCodes.ManualRefund, "bank-ref");

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        topUp.ReviewResolvedAtUtc.Should().BeNull();
    }
}
