using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.Purchases.BackgroundServices;

public class CoursePurchaseNotificationRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CoursePurchaseNotificationRetryBackgroundService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15);
    private const int MaxAttemptsCap = 5;
    private const int BatchSize = 50;

    public CoursePurchaseNotificationRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CoursePurchaseNotificationRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CoursePurchaseNotificationRetryBackgroundService started.");

        using var timer = new PeriodicTimer(_period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessFailedPurchasesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during CoursePurchase notification retry cycle.");
            }
        }

        _logger.LogInformation("CoursePurchaseNotificationRetryBackgroundService stopped.");
    }

    private async Task ProcessFailedPurchasesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var purchaseRepo = scope.ServiceProvider.GetRequiredService<ICoursePurchaseRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var tarhElahiClient = scope.ServiceProvider.GetRequiredService<ITarhElahiIntegrationClient>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var failedPurchases = await purchaseRepo.GetFailedNotificationsForRetryAsync(MaxAttemptsCap, BatchSize, stoppingToken);
        if (failedPurchases.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Found {Count} failed course purchase notifications to retry.",
            failedPurchases.Count);

        foreach (var purchase in failedPurchases)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var user = await userRepo.GetByIdAsync(purchase.BuyerId, stoppingToken);
            if (user is null)
            {
                _logger.LogCritical(
                    "Buyer user {UserId} not found for CoursePurchase {PurchaseId}. Cannot retry notification.",
                    purchase.BuyerId.Value,
                    purchase.Id.Value);
                purchase.RecordNotificationFailure("Buyer user not found in ByeMoney.");
                purchaseRepo.Update(purchase);
                continue;
            }

            var notificationDto = new CoursePurchaseNotificationDto
            {
                EventId = Guid.NewGuid(),
                PurchaseId = purchase.Id.Value,
                BuyerExternalUserId = user.ExternalUserId,
                Item = new TarhElahiNotificationItemDto
                {
                    Type = TarhElahiCatalogItemType.Course,
                    ExternalId = purchase.Snapshot.ExternalProductId,
                    ParentExternalId = null
                },
                Snapshot = new TarhElahiNotificationSnapshotDto
                {
                    ProductSource = purchase.Snapshot.ProductSource,
                    ProductTitle = purchase.Snapshot.ProductTitle,
                    PriceRial = purchase.Snapshot.PriceInRialAtPurchaseTime,
                    PriceNoor = purchase.Snapshot.PriceInNoorAtPurchaseTime,
                    ConversionRate = purchase.Snapshot.ConversionRateAtPurchaseTime
                },
                PurchasedAtUtc = purchase.Snapshot.PurchasedAt
            };

            try
            {
                var success = await tarhElahiClient.NotifyCoursePurchaseAsync(notificationDto, stoppingToken);
                if (success)
                {
                    purchase.MarkNotificationSent();
                    _logger.LogInformation(
                        "Successfully retried and sent course purchase notification for PurchaseId {PurchaseId}.",
                        purchase.Id.Value);
                }
                else
                {
                    purchase.RecordNotificationFailure("TarhElahi notification endpoint returned non-success response on retry.");
                    CheckMaxAttemptsCap(purchase);
                }
            }
            catch (Exception ex)
            {
                purchase.RecordNotificationFailure(ex.Message);
                CheckMaxAttemptsCap(purchase);
            }

            purchaseRepo.Update(purchase);
        }

        await unitOfWork.SaveChangesAsync(stoppingToken);
    }

    private void CheckMaxAttemptsCap(Domain.Modules.Purchases.CoursePurchase purchase)
    {
        if (purchase.NotificationAttempts >= MaxAttemptsCap)
        {
            _logger.LogCritical(
                "CoursePurchase {PurchaseId} has reached maximum retry attempts ({MaxAttempts}). Manual operator intervention is required. Last error: {Reason}",
                purchase.Id.Value,
                MaxAttemptsCap,
                purchase.NotificationFailureReason);
        }
    }
}