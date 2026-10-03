using ByeMoney.Application.Modules.Wallet.Events;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandHandler(
    ITopUpRequestRepository topUpRequestRepository,
    ITopUpSettlementService settlementService,
    IPublisher publisher) : IRequestHandler<ConfirmTopUpCommand, Result>
{
    public async Task<Result> Handle(ConfirmTopUpCommand request, CancellationToken ct)
    {
        var isGateway = request.GatewayName is not null;
        var topUp = isGateway
            ? await topUpRequestRepository.GetByClientReferenceCodeAsync(request.ClientReferenceCode!, ct)
            : await topUpRequestRepository.GetByIdAsync(request.TopUpRequestId, ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                isGateway ? request.ClientReferenceCode : request.TopUpRequestId.Value),
                isGateway ? GatewayTopUpErrorCodes.TopUpNotFound : null);
        if (!isGateway && topUp.PaymentMethod == PaymentMethod.Gateway)
            return Result.Conflict(ApplicationErrors.TopUpRequest_PaymentMethodInvalid);
        if (isGateway && topUp.PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(ApplicationErrors.TopUpRequest_PaymentMethodInvalid,
                GatewayTopUpErrorCodes.InvalidVerificationData);
        if (isGateway && (topUp.AmountRial is null || request.OriginalAmountRial != topUp.AmountRial ||
            request.AffectiveAmountRial != topUp.AmountRial))
            return Result.Failure(string.Format(ApplicationErrors.TopUpRequest_AmountMismatch,
                request.OriginalAmountRial, topUp.AmountRial), GatewayTopUpErrorCodes.TopUpAmountMismatch);
        if (!isGateway && request.ConfirmedAmount != topUp.Amount)
            return Result.Failure(string.Format(ApplicationErrors.TopUpRequest_AmountMismatch, request.ConfirmedAmount, topUp.Amount));

        if (isGateway)
        {
            var other = await topUpRequestRepository.GetByExternalTransactionIdAsync(request.ExternalTransactionId, ct);
            if (other is not null && other.Id != topUp.Id && other.PaymentMethod == PaymentMethod.Gateway)
                return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                    GatewayTopUpErrorCodes.RefNumConflict);
        }

        var wasPending = topUp.Status == TopUpStatus.Pending;
        var result = isGateway
            ? topUp.ConfirmGateway(request.ExternalTransactionId, request.BankReferenceNumber!, request.GatewayName!)
            : topUp.Confirm(request.ExternalTransactionId);
        if (result.IsFailure)
            return isGateway
                ? Result.Conflict(result.ErrorMessage!, topUp.Status == TopUpStatus.Rejected
                    ? GatewayTopUpErrorCodes.TopUpRequiresReview
                    : GatewayTopUpErrorCodes.RefNumConflict)
                : result;

        if (wasPending)
        {
            topUpRequestRepository.Update(topUp);
            await settlementService.SettleAsync(topUp, ct);
            if (topUp.PendingItems.Count > 0)
                await publisher.Publish(new TopUpConfirmed(topUp.Id, topUp.UserId, topUp.Amount, topUp.PendingItems), ct);
        }
        return Result.Success();
    }
}
