using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ByeMoney.UnitTests;

public class ManualGatewayReviewTests
{
    private static TopUpRequest TopUp()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 1000m);
        topUp.OpenFinancialReview("case-1", FinancialReviewReasonCodes.NoCallback);
        return topUp;
    }

    private static FinancialReviewEvidence Evidence(int revision = 1) => new(Guid.NewGuid(), revision,
        new DateOnly(2026, 9, 30), false, "گزارش بانک بررسی شد", null, null, null, null);

    [Fact]
    public void NoMatchingDepositClosesPendingWithoutChangingMoneyState()
    {
        var topUp = TopUp();
        var actor = Guid.NewGuid();
        var evidence = Evidence();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, evidence, actor, "کارمند")
            .IsSuccess.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Pending);
        topUp.ExternalTransactionId.Should().BeNull();
        topUp.GatewayResultKind.Should().BeNull();
        topUp.ReviewAudit.Should().ContainSingle().Which.ActorUserId.Should().Be(actor);
        topUp.ReviewAudit.Single().Evidence.Should().Be(evidence);
    }

    [Fact]
    public void DuplicateIsIdempotentButChangingActorOrNoteConflicts()
    {
        var topUp = TopUp(); var actor = Guid.NewGuid(); var evidence = Evidence();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, evidence, actor, "اول");
        var time = topUp.ReviewResolvedAtUtc;
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, evidence, actor, "نام تازه")
            .IsSuccess.Should().BeTrue();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, evidence, Guid.NewGuid(), "دوم")
            .IsFailure.Should().BeTrue();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, evidence with { Note = "تغییر" }, actor, "اول")
            .IsFailure.Should().BeTrue();
        topUp.ReviewResolvedAtUtc.Should().Be(time);
        topUp.ReviewAudit.Should().ContainSingle().Which.ActorName.Should().Be("اول");
    }

    [Fact]
    public void ReopenPreservesAuditAndRejectsStaleForm()
    {
        var topUp = TopUp(); var actor = Guid.NewGuid();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, Evidence(), actor, null);
        topUp.ReopenFinancialReview("case-1", "new-evidence", "مدرک تازه").IsSuccess.Should().BeTrue();
        topUp.ReopenFinancialReview("case-1", "new-evidence", "مدرک تازه").IsSuccess.Should().BeTrue();
        topUp.ReviewRevision.Should().Be(2);
        topUp.ReviewResolvedAtUtc.Should().BeNull();
        topUp.ReviewAudit.Should().HaveCount(2);
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, Evidence(), actor, null)
            .IsFailure.Should().BeTrue();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null, Evidence(2), actor, null)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ReportDepositIdAloneCannotConfirmPendingPayment()
    {
        var topUp = TopUp();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed, "report-deposit-id",
            Evidence() with { MatchingDepositFound = true, DepositReference = "report-deposit-id", DepositDate = new(2026, 9, 30), DepositAmountRial = 10000 },
            Guid.NewGuid(), null).IsFailure.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Pending);
        topUp.ExternalTransactionId.Should().BeNull();
    }

    [Fact]
    public void ConfirmedPaymentCanCloseOnlyWithItsVerifiedRefNum()
    {
        var topUp = TopUp();
        topUp.RecordGatewayResult("event-1", "SEP", GatewayResultKinds.Verified, "real-ref", "real-rrn", "0", 10000, 10000, DateTime.UtcNow);
        topUp.ConfirmGateway("real-ref", "real-rrn", "SEP");
        var evidence = Evidence() with { MatchingDepositFound = true, DepositReference = "deposit-reference", DepositDate = new(2026, 9, 30), DepositAmountRial = 10000 };
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed, "deposit-reference", evidence, Guid.NewGuid(), null)
            .IsFailure.Should().BeTrue();
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.PaidAndConfirmed, "real-ref", evidence, Guid.NewGuid(), null)
            .IsSuccess.Should().BeTrue();
        topUp.BankReferenceNumber.Should().Be("real-rrn");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualRefundProtectsUncreditedTopUpsEvenAfterReopen(bool rejected)
    {
        var topUp = TopUp();
        if (rejected) topUp.Reject("پرداخت ناموفق");
        var status = topUp.Status;
        topUp.ResolveManualReview("case-1", FinancialReviewOutcomeCodes.ManualRefund, null,
            Evidence() with { ManualRefundReference = "refund-42" }, Guid.NewGuid(), null).IsSuccess.Should().BeTrue();
        topUp.ReopenFinancialReview("case-1", "late-verified", null);
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);
        var settlement = new Mock<ITopUpSettlementService>();
        var service = new GatewayTopUpService(repository.Object, settlement.Object, Mock.Of<IUnitOfWork>(),
            Mock.Of<IPublisher>(), Mock.Of<IServiceScopeFactory>());
        var result = await service.RecordResultAsync(new RecordGatewayResultCommand(topUp.ClientReferenceCode, "SEP", "late", "Verified",
            "ref", "rrn", "0", 10000, 10000, DateTime.UtcNow));
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        (await service.ConfirmAsync(new ConfirmGatewayTopUpCommand(topUp.ClientReferenceCode, "SEP", "ref", "rrn", 10000, 10000)))
            .Result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        topUp.Status.Should().Be(status);
        settlement.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfirmedRefundKeepsExistingUnsupportedError()
    {
        var topUp = TopUp(); topUp.ConfirmGateway("ref", "rrn", "SEP");
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);
        var service = new GatewayTopUpService(repository.Object, Mock.Of<ITopUpSettlementService>(), Mock.Of<IUnitOfWork>(),
            Mock.Of<IPublisher>(), Mock.Of<IServiceScopeFactory>());
        var result = await service.ResolveManualReviewAsync(topUp.ClientReferenceCode, "case-1", FinancialReviewOutcomeCodes.ManualRefund,
            null, Evidence() with { ManualRefundReference = "refund-42" }, Guid.NewGuid(), null);
        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        topUp.ReviewResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CheckedReportAndExplicitMatchAnswerAreRequired()
    {
        var validator = new ResolveGatewayReviewCommandValidator();
        var command = new ResolveGatewayReviewCommand("TR-123", "case-1", FinancialReviewOutcomeCodes.NoMatchingDeposit, null);
        (await validator.ValidateAsync(command)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(command with { Evidence = Evidence() })).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(command with { Evidence = Evidence() with { MatchingDepositFound = null } })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(command with { Evidence = Evidence() with { CheckedReportDate = default } })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(command with { OutcomeCode = FinancialReviewOutcomeCodes.ManualRefund, Evidence = Evidence() })).IsValid.Should().BeFalse();
    }

    [Fact]
    public void BothResolveUrlsRequireFinancialPermissionWithoutAnonymousBypass()
    {
        typeof(AdminGatewayReviewController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Should().Contain(x => x.Policy == PolicyNames.RequireTopUpReview);
        typeof(AdminGatewayReviewController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
        typeof(GatewayTopUpController).GetMethod("ResolveReview").Should().BeNull();
    }
}
