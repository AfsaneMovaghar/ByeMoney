using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;

public sealed class AdminCancelPhysicalPurchaseCommandHandler(
    PhysicalCancellationService service,
    ICurrentUserService currentUser) :
    IRequestHandler<AdminCancelPhysicalPurchaseCommand, Result<PhysicalCancellationResponse>>
{
    public Task<Result<PhysicalCancellationResponse>> Handle(
        AdminCancelPhysicalPurchaseCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.CancelForAdminAsync(
            adminId, request.PurchaseId, request.Quantity, request.OperationId, request.Reason, ct);
    }
}
