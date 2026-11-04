using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminPhysicalCancellationPreview;

public sealed record GetAdminPhysicalCancellationPreviewQuery(Guid PurchaseId, int Quantity) :
    IRequest<Result<PhysicalCancellationPreviewResponse>>;
