using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;

public sealed class CancelMyPhysicalOrderCommandHandler(
    PhysicalCancellationService service,
    ICurrentUserService currentUser) :
    IRequestHandler<CancelMyPhysicalOrderCommand, Result<PhysicalCancellationResponse>>
{
    public Task<Result<PhysicalCancellationResponse>> Handle(CancelMyPhysicalOrderCommand request, CancellationToken ct)
    {
        var buyerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.CancelForBuyerAsync(buyerId, request.CartId, request.OperationId, request.Reason, ct);
    }
}
