using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.RetryPhysicalRestock;

public sealed class RetryPhysicalRestockCommandHandler(PhysicalRestockService service) :
    IRequestHandler<RetryPhysicalRestockCommand, Result<PhysicalRestockActionResponse>>
{
    public Task<Result<PhysicalRestockActionResponse>> Handle(
        RetryPhysicalRestockCommand request, CancellationToken ct)
    {
        return service.RetryNowAsync(request.OperationId, ct);
    }
}
