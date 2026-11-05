using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

/// <summary>
/// برگشت موجودی استرپی پس از commit برگشت نور (I09-PC). نیاز به برگشت موجودی در همان تراکنش لغو پایدار شده است؛
/// این سرویس فقط پس از آن فراخوانی را انجام می‌دهد. شکست استرپی برگشت نور را برنمی‌گرداند و عملیات در صف
/// تلاش مجدد می‌ماند؛ چون استرپی با <c>operationId</c> idempotent است، تکرار اثر دوباره ندارد.
/// </summary>
public sealed class PhysicalRestockService(
    IPhysicalCancellationStore store,
    IPhysicalStockClient stock,
    PhysicalRestockSettings settings)
{
    public async Task RestockAsync(Guid operationId, CancellationToken ct)
    {
        var request = await store.FindPendingRestockAsync(operationId, ct);
        if (request is null)
        {
            return;
        }

        var result = await stock.RestockAsync(request, ct);
        if (result.IsSuccess)
        {
            await store.MarkStockReturnedAsync(operationId, ct);
            return;
        }

        await store.RecordStockReturnFailureAsync(operationId, result.ErrorCode, ct);
    }

    public async Task RetryPendingAsync(int batchSize, CancellationToken ct)
    {
        var operations = await store.GetPendingRestockOperationsAsync(batchSize, settings.MaxAttempts, ct);
        foreach (var operationId in operations)
        {
            await RestockAsync(operationId, ct);
        }
    }
}
