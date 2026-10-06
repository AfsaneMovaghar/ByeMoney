using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Events;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Resources;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Wallet.Services;

public sealed class GatewayTopUpService(
    ITopUpRequestRepository topUpRequestRepository,
    ITopUpSettlementService settlementService,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IServiceScopeFactory scopeFactory) : IGatewayTopUpService
{
    private static GatewayReviewState ReviewState(TopUpRequest topUp) => new(
        topUp.ClientReferenceCode, topUp.ReviewCaseId!, topUp.Status.ToString(),
        topUp.ReviewReasonCode!, topUp.ReviewOpenedAtUtc!.Value, topUp.ReviewResolvedAtUtc,
        topUp.ReviewOutcomeCode, topUp.ReviewResolutionFinancialReferenceId, topUp.ReviewRevision,
        topUp.ReviewAudit, topUp.ManualRefundReference);

    public async Task<Result<GatewayReviewState>> ResolveManualReviewAsync(string clientReferenceCode, string caseId,
        string outcomeCode, string? financialReferenceId, FinancialReviewEvidence evidence,
        Guid actorUserId, string? actorName, CancellationToken ct = default)
    {
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (topUp is null) return Result<GatewayReviewState>.NotFound(ApplicationErrors.TopUpRequest_NotFound,
            GatewayTopUpErrorCodes.TopUpNotFound);
        if (outcomeCode == FinancialReviewOutcomeCodes.ManualRefund && topUp.Status == TopUpStatus.Confirmed)
            return Result<GatewayReviewState>.Failure(ApplicationErrors.TopUpRequest_ReviewRefundUnsupported,
                GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        if (evidence.MatchingDepositFound == true && evidence.DepositAmountRial != topUp.AmountRial)
            return Result<GatewayReviewState>.Failure(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid,
                GatewayTopUpErrorCodes.TopUpAmountMismatch);
        var result = topUp.ResolveManualReview(caseId, outcomeCode, financialReferenceId, evidence, actorUserId, actorName);
        if (result.IsFailure) return Result<GatewayReviewState>.Conflict(result.ErrorMessage!, GatewayTopUpErrorCodes.ReviewConflict);
        topUpRequestRepository.Update(topUp);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            using var scope = scopeFactory.CreateScope();
            var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
                .GetByClientReferenceCodeAsync(clientReferenceCode, ct);
            var prior = fresh?.ReviewAudit.FirstOrDefault(x => x.EventId == evidence.OperationId.ToString());
            if (fresh is not null && prior?.Evidence == evidence && prior.ActorUserId == actorUserId &&
                prior.OutcomeCode == outcomeCode && prior.FinancialReferenceId == financialReferenceId)
                return Result<GatewayReviewState>.Success(ReviewState(fresh));
            return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict, GatewayTopUpErrorCodes.ReviewConflict);
        }
        return Result<GatewayReviewState>.Success(ReviewState(topUp));
    }

    public async Task<Result<GatewayReviewState>> ReopenReviewAsync(string clientReferenceCode, string caseId,
        string evidenceId, string? note, CancellationToken ct = default)
    {
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (topUp is null) return Result<GatewayReviewState>.NotFound(ApplicationErrors.TopUpRequest_NotFound,
            GatewayTopUpErrorCodes.TopUpNotFound);
        var result = topUp.ReopenFinancialReview(caseId, evidenceId, note);
        if (result.IsFailure) return Result<GatewayReviewState>.Conflict(result.ErrorMessage!, GatewayTopUpErrorCodes.ReviewConflict);
        topUpRequestRepository.Update(topUp);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict, GatewayTopUpErrorCodes.ReviewConflict); }
        return Result<GatewayReviewState>.Success(ReviewState(topUp));
    }

    public async Task<Result<GatewayReviewState>> OpenReviewAsync(string clientReferenceCode,
        string caseId, string reasonCode, CancellationToken ct = default)
    {
        var owner = await topUpRequestRepository.GetByReviewCaseIdAsync(caseId, ct);
        if (owner is not null && owner.ClientReferenceCode != clientReferenceCode)
            return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict,
                GatewayTopUpErrorCodes.ReviewConflict);
        var topUp = owner ?? await topUpRequestRepository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (topUp is null)
            return Result<GatewayReviewState>.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                clientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);

        var alreadyOpen = topUp.ReviewCaseId is not null;
        var opened = topUp.OpenFinancialReview(caseId, reasonCode);
        if (opened.IsFailure)
            return Result<GatewayReviewState>.Conflict(opened.ErrorMessage!, GatewayTopUpErrorCodes.ReviewConflict);
        if (alreadyOpen)
            return Result<GatewayReviewState>.Success(ReviewState(topUp));

        topUpRequestRepository.Update(topUp);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return Result<GatewayReviewState>.Success(ReviewState(topUp));
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RetryOpenInFreshScopeAsync(clientReferenceCode, caseId, reasonCode, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_TopUpRequests_ReviewCaseId" })
        {
            return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict,
                GatewayTopUpErrorCodes.ReviewConflict);
        }
    }

    public async Task<Result<GatewayReviewState>> ResolveReviewAsync(string clientReferenceCode,
        string caseId, string outcomeCode, string? financialReferenceId, CancellationToken ct = default)
    {
        if (outcomeCode == FinancialReviewOutcomeCodes.ManualRefund)
            return Result<GatewayReviewState>.Failure(ApplicationErrors.TopUpRequest_ReviewRefundUnsupported,
                GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (topUp is null)
            return Result<GatewayReviewState>.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                clientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);
        var alreadyResolved = topUp.ReviewResolvedAtUtc is not null;
        var resolved = topUp.ResolveFinancialReview(caseId, outcomeCode, financialReferenceId);
        if (resolved.IsFailure)
            return Result<GatewayReviewState>.Conflict(resolved.ErrorMessage!,
                topUp.ReviewCaseId == caseId && !alreadyResolved
                    ? GatewayTopUpErrorCodes.ReviewStateMismatch : GatewayTopUpErrorCodes.ReviewConflict);
        if (!alreadyResolved)
        {
            topUpRequestRepository.Update(topUp);
            try { await unitOfWork.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                return await ResolveAfterConcurrencyAsync(clientReferenceCode, caseId,
                    outcomeCode, financialReferenceId, ct);
            }
        }
        return Result<GatewayReviewState>.Success(ReviewState(topUp));
    }

    private async Task<Result<GatewayReviewState>> RetryOpenInFreshScopeAsync(string clientReferenceCode,
        string caseId, string reasonCode, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var fresh = await repository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (fresh is null)
            return Result<GatewayReviewState>.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                clientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);
        var alreadyOpen = fresh.ReviewCaseId is not null;
        var opened = fresh.OpenFinancialReview(caseId, reasonCode);
        if (opened.IsFailure)
            return Result<GatewayReviewState>.Conflict(opened.ErrorMessage!, GatewayTopUpErrorCodes.ReviewConflict);
        if (!alreadyOpen)
        {
            repository.Update(fresh);
            try { await work.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict,
                    GatewayTopUpErrorCodes.ReviewConflict);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_TopUpRequests_ReviewCaseId" })
            {
                return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict,
                    GatewayTopUpErrorCodes.ReviewConflict);
            }
        }
        return Result<GatewayReviewState>.Success(ReviewState(fresh));
    }

    private async Task<Result<GatewayReviewState>> ResolveAfterConcurrencyAsync(string clientReferenceCode,
        string caseId, string outcomeCode, string? financialReferenceId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>();
        var fresh = await repository.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (fresh is not null && fresh.ReviewCaseId == caseId &&
            fresh.ReviewResolvedAtUtc is not null && fresh.ReviewOutcomeCode == outcomeCode &&
            fresh.ReviewResolutionFinancialReferenceId == financialReferenceId)
            return Result<GatewayReviewState>.Success(ReviewState(fresh));
        return Result<GatewayReviewState>.Conflict(DomainErrors.TopUpRequest_ReviewConflict,
            GatewayTopUpErrorCodes.ReviewConflict);
    }
    public async Task<Result> RecordResultAsync(RecordGatewayResultCommand command, CancellationToken ct = default)
    {
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(command.ClientReferenceCode, ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                command.ClientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);
        if (topUp.PaymentMethod != PaymentMethod.Gateway || command.Gateway != "SEP")
            return Result.Conflict(ApplicationErrors.TopUpRequest_PaymentMethodInvalid,
                GatewayTopUpErrorCodes.InvalidVerificationData);
        if (topUp.ManualRefundReference is not null && command.Kind is GatewayResultKinds.Verified or GatewayResultKinds.ReverseSucceeded)
            return Result.Conflict(ApplicationErrors.TopUpRequest_ReviewRefundUnsupported,
                GatewayTopUpErrorCodes.ReviewRefundUnsupported);
        var repeated = topUp.GatewayEventId == command.EventId;
        if (!repeated && topUp.Status == TopUpStatus.Confirmed && command.Kind == GatewayResultKinds.Verified &&
            (topUp.AmountRial != command.OriginalAmountRial ||
             topUp.AmountRial != command.AffectiveAmountRial))
            return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.TopUpAlreadyConfirmed);

        if (command.BankTransactionId is not null)
        {
            var owner = await topUpRequestRepository.GetByExternalTransactionIdAsync(command.BankTransactionId, ct);
            if (owner is not null && owner.Id != topUp.Id)
                return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                    GatewayTopUpErrorCodes.RefNumConflict);
        }

        var recorded = topUp.RecordGatewayResult(command.EventId, command.Gateway, command.Kind,
            command.BankTransactionId, command.BankReferenceNumber, command.BankResultCode,
            command.OriginalAmountRial, command.AffectiveAmountRial, command.OccurredAtUtc);
        if (recorded.IsFailure)
            return Result.Conflict(recorded.ErrorMessage!, repeated
                ? GatewayTopUpErrorCodes.GatewayResultConflict
                : GatewayTopUpErrorCodes.TopUpRequiresReview);
        if (command.Kind == GatewayResultKinds.Unknown && topUp.Status != TopUpStatus.Pending)
            return Result.Success();
        if (repeated && command.Kind != GatewayResultKinds.Verified)
            return Result.Success();

        if (command.Kind == GatewayResultKinds.Verified &&
            (topUp.AmountRial is null || topUp.AmountRial != command.OriginalAmountRial ||
             topUp.AmountRial != command.AffectiveAmountRial))
        {
            if (!repeated)
            {
                var saveResult = await SaveTopUpAsync(topUp, ct);
                if (saveResult.IsFailure)
                    return saveResult;
            }
            return Result.Failure(string.Format(ApplicationErrors.TopUpRequest_AmountMismatch,
                command.OriginalAmountRial, topUp.AmountRial), GatewayTopUpErrorCodes.TopUpAmountMismatch);
        }

        if (command.Kind is GatewayResultKinds.Unpaid or GatewayResultKinds.ReverseSucceeded &&
            topUp.Status == TopUpStatus.Pending)
        {
            topUp.Reject(command.Kind == GatewayResultKinds.Unpaid
                ? ApplicationErrors.TopUpRequest_GatewayUnpaid
                : ApplicationErrors.TopUpRequest_GatewayReversed);
        }

        if (command.Kind == GatewayResultKinds.Verified)
        {
            var outcome = await ConfirmAsync(new ConfirmGatewayTopUpCommand(command.ClientReferenceCode,
                command.Gateway, command.BankTransactionId!, command.BankReferenceNumber!,
                command.OriginalAmountRial!.Value, command.AffectiveAmountRial!.Value), ct);
            return outcome.Result;
        }

        if (!repeated)
        {
            var saveResult = await SaveTopUpAsync(topUp, ct);
            if (saveResult.IsFailure)
                return saveResult;
        }

        return Result.Success();
    }

    private async Task<Result> SaveTopUpAsync(TopUpRequest topUp, CancellationToken ct)
    {
        topUpRequestRepository.Update(topUp);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.TopUpConcurrentConfirmation);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_TopUpRequests_ExternalTransactionId" })
        {
            return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.RefNumConflict);
        }
    }

    public async Task<GatewayConfirmationOutcome> ConfirmAsync(ConfirmGatewayTopUpCommand command, CancellationToken ct = default)
    {
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(command.ClientReferenceCode, ct);
        if (topUp is null)
            return new GatewayConfirmationOutcome(Result.NotFound(
                string.Format(ApplicationErrors.TopUpRequest_NotFound, command.ClientReferenceCode),
                GatewayTopUpErrorCodes.TopUpNotFound));

        if (topUp.PaymentMethod != PaymentMethod.Gateway)
            return new GatewayConfirmationOutcome(Result.Conflict(
                ApplicationErrors.TopUpRequest_PaymentMethodInvalid,
                GatewayTopUpErrorCodes.InvalidVerificationData));
        if (topUp.ManualRefundReference is not null)
            return new GatewayConfirmationOutcome(Result.Conflict(ApplicationErrors.TopUpRequest_ReviewRefundUnsupported,
                GatewayTopUpErrorCodes.ReviewRefundUnsupported));

        if (topUp.AmountRial is null ||
            command.OriginalAmountRial != topUp.AmountRial ||
            command.AffectiveAmountRial != topUp.AmountRial)
            return new GatewayConfirmationOutcome(Result.Failure(
                string.Format(ApplicationErrors.TopUpRequest_AmountMismatch, command.OriginalAmountRial, topUp.AmountRial),
                GatewayTopUpErrorCodes.TopUpAmountMismatch));

        var other = await topUpRequestRepository.GetByExternalTransactionIdAsync(command.ExternalTransactionId, ct);
        if (other is not null && other.Id != topUp.Id && other.PaymentMethod == PaymentMethod.Gateway)
            return new GatewayConfirmationOutcome(Result.Conflict(
                ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.RefNumConflict));

        if (topUp.Status == TopUpStatus.Confirmed)
        {
            if (topUp.ExternalTransactionId == command.ExternalTransactionId &&
                topUp.BankReferenceNumber == command.BankReferenceNumber &&
                topUp.GatewayName == command.Gateway &&
                topUp.AmountRial == command.OriginalAmountRial &&
                topUp.AmountRial == command.AffectiveAmountRial)
                return new GatewayConfirmationOutcome(Result.Success(), Idempotent: true);

            return new GatewayConfirmationOutcome(Result.Conflict(
                DomainErrors.TopUpRequest_AlreadyConfirmedDifferentExternalId,
                GatewayTopUpErrorCodes.RefNumConflict));
        }

        if (topUp.Status == TopUpStatus.Rejected)
            return new GatewayConfirmationOutcome(Result.Conflict(
                DomainErrors.TopUpRequest_CannotConfirmRejected,
                GatewayTopUpErrorCodes.TopUpRequiresReview));

        if (topUp.Status == TopUpStatus.Unresolved)
            return new GatewayConfirmationOutcome(Result.Conflict(
                DomainErrors.TopUpRequest_ReviewStateMismatch, GatewayTopUpErrorCodes.TopUpRequiresReview));

        if (topUp.Status != TopUpStatus.Pending)
            return new GatewayConfirmationOutcome(Result.Conflict(
                string.Format(DomainErrors.TopUpRequest_CannotConfirmStatus, topUp.Status),
                GatewayTopUpErrorCodes.RefNumConflict));

        var confirmResult = topUp.ConfirmGateway(command.ExternalTransactionId, command.BankReferenceNumber, command.Gateway);
        if (confirmResult.IsFailure)
            return new GatewayConfirmationOutcome(Result.Conflict(
                confirmResult.ErrorMessage!,
                topUp.Status == TopUpStatus.Rejected ? GatewayTopUpErrorCodes.TopUpRequiresReview : GatewayTopUpErrorCodes.RefNumConflict));

        topUpRequestRepository.Update(topUp);

        try
        {
            await settlementService.SettleAsync(topUp, ct);
            if (topUp.PendingItems.Count > 0)
                await publisher.Publish(new TopUpConfirmed(topUp.Id, topUp.UserId, topUp.Amount, topUp.PendingItems), ct);

            return new GatewayConfirmationOutcome(Result.Success());
        }
        catch (DbUpdateConcurrencyException)
        {
            return await HandleConfirmationConcurrencyAsync(command, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_TopUpRequests_ExternalTransactionId" })
        {
            return new GatewayConfirmationOutcome(Result.Conflict(
                ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.RefNumConflict));
        }
    }

    public async Task<Result> CancelAsync(CancelGatewayTopUpCommand command, CancellationToken ct = default)
    {
        var topUp = await topUpRequestRepository.GetByClientReferenceCodeAsync(command.ClientReferenceCode, ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                command.ClientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);

        if (topUp.PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(ApplicationErrors.TopUpRequest_PaymentMethodInvalid,
                GatewayTopUpErrorCodes.InvalidVerificationData);

        if (topUp.Status == TopUpStatus.Rejected)
            return topUp.GatewayName == command.Gateway
                ? Result.Success()
                : Result.Conflict(ApplicationErrors.TopUpRequest_CannotCancelRejected,
                    GatewayTopUpErrorCodes.TopUpRequiresReview);

        if (topUp.Status == TopUpStatus.Confirmed)
            return Result.Conflict(string.Format(DomainErrors.TopUpRequest_CannotRejectStatus, topUp.Status),
                GatewayTopUpErrorCodes.TopUpAlreadyConfirmed);

        if (topUp.Status is TopUpStatus.Unresolved or TopUpStatus.ManuallyRefunded)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch,
                GatewayTopUpErrorCodes.TopUpRequiresReview);

        try
        {
            topUp.CancelGateway(command.Gateway, ApplicationErrors.TopUpRequest_GatewayCancelled);
            topUpRequestRepository.Update(topUp);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return await HandleCancellationConcurrencyAsync(command, ct);
        }
    }

    private async Task<GatewayConfirmationOutcome> HandleConfirmationConcurrencyAsync(
        ConfirmGatewayTopUpCommand command, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
            .GetByClientReferenceCodeAsync(command.ClientReferenceCode, ct);

        if (fresh is { Status: TopUpStatus.Confirmed } &&
            fresh.ExternalTransactionId == command.ExternalTransactionId &&
            fresh.BankReferenceNumber == command.BankReferenceNumber &&
            fresh.GatewayName == command.Gateway &&
            fresh.AmountRial == command.OriginalAmountRial &&
            fresh.AmountRial == command.AffectiveAmountRial)
            return new GatewayConfirmationOutcome(Result.Success(), Idempotent: true);

        return new GatewayConfirmationOutcome(Result.Conflict(
            ApplicationErrors.TopUpRequest_IdempotencyConflict,
            GatewayTopUpErrorCodes.TopUpConcurrentConfirmation));
    }

    private async Task<Result> HandleCancellationConcurrencyAsync(
        CancelGatewayTopUpCommand command, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
            .GetByClientReferenceCodeAsync(command.ClientReferenceCode, ct);

        return fresh?.Status switch
        {
            TopUpStatus.Rejected when fresh.GatewayName == command.Gateway => Result.Success(),
            TopUpStatus.Rejected => Result.Conflict(ApplicationErrors.TopUpRequest_CannotCancelRejected,
                GatewayTopUpErrorCodes.TopUpRequiresReview),
            TopUpStatus.Confirmed => Result.Conflict(
                string.Format(DomainErrors.TopUpRequest_CannotRejectStatus, TopUpStatus.Confirmed),
                GatewayTopUpErrorCodes.TopUpAlreadyConfirmed),
            null => Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                command.ClientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound),
            _ => Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.TopUpConcurrentConfirmation)
        };
    }
}
