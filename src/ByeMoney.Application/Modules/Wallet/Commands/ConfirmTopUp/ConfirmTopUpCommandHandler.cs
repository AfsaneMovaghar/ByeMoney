using ByeMoney.Application.Modules.Wallet.Events;
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
        var topUp = await topUpRequestRepository.GetByIdAsync(request.TopUpRequestId, ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound, request.TopUpRequestId.Value));
        if (request.ConfirmedAmount != topUp.Amount)
            return Result.Failure(string.Format(ApplicationErrors.TopUpRequest_AmountMismatch, request.ConfirmedAmount, topUp.Amount));

        var wasPending = topUp.Status == TopUpStatus.Pending;
        var result = topUp.Confirm(request.ExternalTransactionId);
        if (result.IsFailure) return result;

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