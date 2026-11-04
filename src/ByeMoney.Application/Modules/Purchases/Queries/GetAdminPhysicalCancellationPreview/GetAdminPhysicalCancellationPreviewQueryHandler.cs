using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminPhysicalCancellationPreview;

public sealed class GetAdminPhysicalCancellationPreviewQueryHandler(PhysicalCancellationService service) :
    IRequestHandler<GetAdminPhysicalCancellationPreviewQuery, Result<PhysicalCancellationPreviewResponse>>
{
    public Task<Result<PhysicalCancellationPreviewResponse>> Handle(
        GetAdminPhysicalCancellationPreviewQuery request, CancellationToken ct) =>
        service.PreviewForAdminAsync(request.PurchaseId, request.Quantity, ct);
}
