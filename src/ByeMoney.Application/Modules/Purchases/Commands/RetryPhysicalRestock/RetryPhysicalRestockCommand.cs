using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.RetryPhysicalRestock;

/// <summary>تلاش فوری ادمین برای برگشت موجودی عملیات رهاشده.</summary>
public sealed record RetryPhysicalRestockCommand(Guid OperationId) : IRequest<Result<PhysicalRestockActionResponse>>;
