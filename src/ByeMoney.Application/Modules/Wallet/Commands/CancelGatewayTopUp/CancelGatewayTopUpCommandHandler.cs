using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;

public sealed class CancelGatewayTopUpCommandHandler(
    ITopUpRequestRepository topUps,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelGatewayTopUpCommand, Result>
{
    private const string SupportedGateway = "SEP";

    public async Task<Result> Handle(CancelGatewayTopUpCommand request, CancellationToken ct)
    {
        var topUp = await topUps.GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                request.ClientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound);
        if (topUp.PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(ApplicationErrors.TopUpRequest_PaymentMethodInvalid,
                GatewayTopUpErrorCodes.InvalidVerificationData);
        if (topUp.Status == TopUpStatus.Rejected)
            return topUp.GatewayName == SupportedGateway
                ? Result.Success()
                : Result.Conflict(DomainErrors.TopUpRequest_CannotConfirmRejected,
                    GatewayTopUpErrorCodes.TopUpRequiresReview);
        if (topUp.Status == TopUpStatus.Confirmed)
            return Result.Conflict(string.Format(DomainErrors.TopUpRequest_CannotRejectStatus, topUp.Status),
                GatewayTopUpErrorCodes.TopUpAlreadyConfirmed);

        topUp.CancelGateway(SupportedGateway, ApplicationErrors.TopUpRequest_GatewayCancelled);
        topUps.Update(topUp);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
