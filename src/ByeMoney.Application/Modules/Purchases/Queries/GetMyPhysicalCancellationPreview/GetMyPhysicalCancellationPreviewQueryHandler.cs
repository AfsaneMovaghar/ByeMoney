using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyPhysicalCancellationPreview;

public sealed class GetMyPhysicalCancellationPreviewQueryHandler(
    PhysicalCancellationService service,
    ICurrentUserService currentUser) :
    IRequestHandler<GetMyPhysicalCancellationPreviewQuery, Result<PhysicalCancellationPreviewResponse>>
{
    public Task<Result<PhysicalCancellationPreviewResponse>> Handle(
        GetMyPhysicalCancellationPreviewQuery request, CancellationToken ct)
    {
        var buyerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.PreviewForBuyerAsync(buyerId, request.CartId, ct);
    }
}
