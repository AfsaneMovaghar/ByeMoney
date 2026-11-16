using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;

/// <summary>
/// تحویل خودکار قلم‌هایی که بیش از مهلت تنظیم‌شده در «ارسال‌شده» مانده‌اند (SHIP-TRACKING).
/// مهلت پشتیبان است: حالت معمول با وضعیت پستکس یا تأیید خریدار بسته می‌شود.
/// </summary>
public sealed class PhysicalDeliveryAutoConfirmBackgroundService(
    IServiceScopeFactory scopes,
    ILogger<PhysicalDeliveryAutoConfirmBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(StartupDelay, ct);
            using var timer = new PeriodicTimer(Period);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var sync = scope.ServiceProvider.GetRequiredService<PhysicalDeliverySyncService>();
                    await sync.ConfirmExpiredAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Physical delivery auto-confirm cycle failed");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }
}
