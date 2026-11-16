using ByeMoney.Application.Modules.Purchases.Delivery.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;

/// <summary>
/// استعلام دوره‌ای وضعیت مرسوله‌های ثبت‌شده نزد واسط‌های پستی و ثبت «تحویل‌شده» (SHIP-TRACKING).
/// یک میزبان برای همهٔ واسط‌ها؛ اگر هیچ واسطی فعال/پیکربندی‌شده نباشد، چرخه بی‌درنگ برمی‌گردد
/// و هیچ درخواستی فرستاده نمی‌شود.
/// </summary>
public sealed class ExternalTrackingBackgroundService(
    IServiceScopeFactory scopes,
    PhysicalDeliverySettings settings,
    ILogger<ExternalTrackingBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!HasConfiguredClient())
        {
            logger.LogInformation("External tracking sync is disabled (no configured provider client).");
            return;
        }

        var period = TimeSpan.FromMinutes(settings.PollIntervalMinutes > 0 ? settings.PollIntervalMinutes : 60);

        try
        {
            await Task.Delay(StartupDelay, ct);
            using var timer = new PeriodicTimer(period);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var sync = scope.ServiceProvider.GetRequiredService<PhysicalDeliverySyncService>();
                    await sync.SyncExternalAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "External tracking sync cycle failed");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during application shutdown.
        }
    }

    /// <summary>واسط‌ها اسکوپ‌دار خوانده می‌شوند تا وابستگی اسکوپ‌دار در سینگلتون قفل نشود.</summary>
    private bool HasConfiguredClient()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider
            .GetServices<ITrackingProviderClient>()
            .Any(client => client.IsConfigured);
    }
}
