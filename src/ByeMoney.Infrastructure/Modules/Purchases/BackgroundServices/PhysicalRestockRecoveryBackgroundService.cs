using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;

/// <summary>
/// تلاش مجدد برگشت موجودی استرپی برای لغوهای فیزیکی که پس از commit برگشت نور تأیید برگشت موجودی نگرفته‌اند
/// (قطعی استرپی یا سبدی که commit استرپی‌اش هنوز همگام نشده). تکرار با همان operationId در استرپی اثر دوباره ندارد.
/// </summary>
public sealed class PhysicalRestockRecoveryBackgroundService(
    IServiceScopeFactory scopes,
    ILogger<PhysicalRestockRecoveryBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Period);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var restock = scope.ServiceProvider.GetRequiredService<PhysicalRestockService>();
                    await restock.RetryPendingAsync(BatchSize, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Physical restock recovery cycle failed");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }
}
