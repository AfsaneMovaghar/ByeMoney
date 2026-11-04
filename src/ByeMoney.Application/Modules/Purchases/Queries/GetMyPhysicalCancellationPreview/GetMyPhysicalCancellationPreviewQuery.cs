using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyPhysicalCancellationPreview;

public sealed record GetMyPhysicalCancellationPreviewQuery(Guid CartId) :
    IRequest<Result<PhysicalCancellationPreviewResponse>>;
