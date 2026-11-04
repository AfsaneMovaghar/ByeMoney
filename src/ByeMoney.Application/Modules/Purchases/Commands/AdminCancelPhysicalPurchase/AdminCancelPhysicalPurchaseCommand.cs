using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;

/// <summary>لغو یک قلم یا بخشی از تعداد آن توسط ادمین مجاز پیش از ارسال (D09).</summary>
public sealed record AdminCancelPhysicalPurchaseCommand(Guid PurchaseId, int Quantity, Guid OperationId, string Reason) :
    IRequest<Result<PhysicalCancellationResponse>>;
