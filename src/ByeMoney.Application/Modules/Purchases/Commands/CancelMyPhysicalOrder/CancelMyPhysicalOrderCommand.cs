using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;

/// <summary>لغو کل بخش فیزیکی سبد کاربر جاری پیش از ارسال (D09).</summary>
public sealed record CancelMyPhysicalOrderCommand(Guid CartId, Guid OperationId, string Reason) :
    IRequest<Result<PhysicalCancellationResponse>>;
