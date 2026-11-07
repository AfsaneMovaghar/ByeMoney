using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

/// <summary>
/// برگشت موجودی استرپی پس از commit برگشت نور (I09-PC). نیاز به برگشت موجودی در همان تراکنش لغو پایدار شده است؛
/// این سرویس فقط پس از آن فراخوانی را انجام می‌دهد. شکست استرپی برگشت نور را برنمی‌گرداند و عملیات در صف
/// تلاش مجدد می‌ماند؛ چون استرپی با <c>operationId</c> idempotent است، تکرار اثر دوباره ندارد.
/// </summary>
public sealed class PhysicalRestockService(
    IPhysicalCancellationStore store,
    IPhysicalStockClient stock,
    PhysicalRestockSettings settings,
    ILogger<PhysicalRestockService> logger)
{
    /// <summary>
    /// یک تلاش دستی ادمین برای برگشت موجودی. شمارندهٔ تلاش‌ها تغییر نمی‌کند: اگر شکست بخورد، عملیات همچنان
    /// رهاشده می‌ماند و بازیابی پس‌زمینه دوباره استرپی را با همان خطای دائمی صدا نمی‌زند.
    /// </summary>
    public async Task<Result<PhysicalRestockActionResponse>> RetryNowAsync(Guid operationId, CancellationToken ct)
    {
        var request = await store.FindPendingRestockAsync(operationId, ct);
        if (request is null)
        {
            return RestockNotFound();
        }

        var result = await stock.RestockAsync(request, ct);
        if (result.IsSuccess)
        {
            await store.MarkStockReturnedAsync(operationId, ct);
            return Result<PhysicalRestockActionResponse>.Success(
                new PhysicalRestockActionResponse(operationId, false, null));
        }

        await store.RecordStockReturnErrorAsync(operationId, result.ErrorCode, ct);
        return Result<PhysicalRestockActionResponse>.Success(
            new PhysicalRestockActionResponse(operationId, true, result.ErrorCode));
    }

    /// <summary>ادمین اعلام می‌کند موجودی را در خود سایت اصلاح کرده است؛ عملیات از صف ناتمام‌ها خارج می‌شود.</summary>
    public async Task<Result<PhysicalRestockActionResponse>> ResolveManuallyAsync(
        Guid adminId, Guid operationId, CancellationToken ct)
    {
        if (await store.MarkStockResolvedManuallyAsync(operationId, ct) == 0)
        {
            return RestockNotFound();
        }

        logger.LogWarning(
            "Physical restock operation {OperationId} was closed manually by admin {AdminId}.", operationId, adminId);
        return Result<PhysicalRestockActionResponse>.Success(new PhysicalRestockActionResponse(operationId, false, null));
    }

    private static Result<PhysicalRestockActionResponse> RestockNotFound() =>
        Result<PhysicalRestockActionResponse>.NotFound(
            PhysicalCancellationErrors.RestockOperationNotFound,
            PhysicalCancellationErrorCodes.NotFound);

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
