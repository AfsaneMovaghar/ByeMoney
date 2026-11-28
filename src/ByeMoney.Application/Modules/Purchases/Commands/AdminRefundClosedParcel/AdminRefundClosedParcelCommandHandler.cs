using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminRefundClosedParcel;

public sealed class AdminRefundClosedParcelCommandHandler(
    ClosedParcelRefundService service,
    ICurrentUserService currentUser) :
    IRequestHandler<AdminRefundClosedParcelCommand, Result<ClosedParcelRefundResponse>>
{
    public Task<Result<ClosedParcelRefundResponse>> Handle(
        AdminRefundClosedParcelCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.RefundAsync(adminId, request.PurchaseId, request.OperationId, request.Reason, ct);
    }
}
