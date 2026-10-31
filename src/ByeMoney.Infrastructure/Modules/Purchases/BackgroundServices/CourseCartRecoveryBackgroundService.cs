using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;

public sealed class CourseCartRecoveryBackgroundService(IServiceScopeFactory scopes,
    ILogger<CourseCartRecoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ICartStore>();
                foreach (var cart in await store.GetPendingAsync(ct))
                {
                    try
                    {
                        using var itemScope = scopes.CreateScope();
                        await itemScope.ServiceProvider.GetRequiredService<CartService>().ProcessAsync(cart, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception ex) { logger.LogError(ex, "Course cart recovery failed for {CartId}", cart.Id); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Course cart recovery cycle failed"); }
            try { if (!await timer.WaitForNextTickAsync(ct)) return; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
