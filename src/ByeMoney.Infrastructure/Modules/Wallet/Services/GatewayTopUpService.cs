using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
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

