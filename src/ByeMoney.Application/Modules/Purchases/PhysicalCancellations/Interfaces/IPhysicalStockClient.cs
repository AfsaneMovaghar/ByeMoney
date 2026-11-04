using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;

/// <summary>
/// برگشت موجودی کالای فیزیکی در استرپی. استرپی با <c>operationId</c> idempotent است و تکرار همان درخواست اثر دوباره ندارد.
/// </summary>
public interface IPhysicalStockClient
{
    Task<Result> RestockAsync(PhysicalRestockRequest request, CancellationToken ct);
}
