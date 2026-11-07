using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolvePhysicalRestock;

public sealed class ResolvePhysicalRestockCommandHandler(
    PhysicalRestockService service,
    ICurrentUserService currentUser) :
    IRequestHandler<ResolvePhysicalRestockCommand, Result<PhysicalRestockActionResponse>>
{
    public Task<Result<PhysicalRestockActionResponse>> Handle(
        ResolvePhysicalRestockCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.ResolveManuallyAsync(adminId, request.OperationId, ct);
    }
}
