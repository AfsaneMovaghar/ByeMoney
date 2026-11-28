using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminRefundClosedParcel;

public sealed record AdminRefundClosedParcelCommand(
    Guid PurchaseId,
    Guid OperationId,
    string Reason) : IRequest<Result<ClosedParcelRefundResponse>>;
