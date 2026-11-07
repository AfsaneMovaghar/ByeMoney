using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolvePhysicalRestock;

/// <summary>بستن دستی برگشت موجودی پس از اصلاح موجودی توسط ادمین.</summary>
public sealed record ResolvePhysicalRestockCommand(Guid OperationId) : IRequest<Result<PhysicalRestockActionResponse>>;
