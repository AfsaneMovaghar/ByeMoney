using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Purchases.Services;

public class CoursePurchaseNotifier : ICoursePurchaseNotifier
{
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly ICoursePurchaseRepository _coursePurchaseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CoursePurchaseNotifier> _logger;

    public CoursePurchaseNotifier(
        ITarhElahiIntegrationClient tarhElahiClient,
        ICoursePurchaseRepository coursePurchaseRepository,
        IUnitOfWork unitOfWork,
        ILogger<CoursePurchaseNotifier> logger)
    {
        _tarhElahiClient = tarhElahiClient;
        _coursePurchaseRepository = coursePurchaseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task NotifyAsync(
        CoursePurchase purchase,
        string buyerExternalUserId,
        TarhElahiCourseDto course,
        ProductSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var notificationDto = new CoursePurchaseNotificationDto
        {
            EventId = Guid.NewGuid(),
            PurchaseId = purchase.Id.Value,
            BuyerExternalUserId = buyerExternalUserId,
            Item = new TarhElahiNotificationItemDto
            {
                Type = TarhElahiCatalogItemType.Course,
                ExternalId = course.ExternalId,
                ParentExternalId = course.ParentExternalId
            },
            Snapshot = new TarhElahiNotificationSnapshotDto
            {
                ProductSource = snapshot.ProductSource,
                ProductTitle = snapshot.ProductTitle,
                PriceRial = snapshot.PriceInRialAtPurchaseTime,
                PriceNoor = snapshot.PriceInNoorAtPurchaseTime,
                ConversionRate = snapshot.ConversionRateAtPurchaseTime
            },
            PurchasedAtUtc = snapshot.PurchasedAt
        };

        try
        {
            var success = await _tarhElahiClient.NotifyCoursePurchaseAsync(notificationDto, cancellationToken);
            if (success)
            {
                purchase.MarkNotificationSent();
            }
            else
            {
                _logger.LogWarning(
                    "Outbound notification to TarhElahi for CoursePurchase {PurchaseId} returned unsuccessful status.",
                    purchase.Id.Value);
                purchase.RecordNotificationFailure("TarhElahi notification endpoint returned non-success response.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception occurred while sending outbound notification to TarhElahi for CoursePurchase {PurchaseId}.",
                purchase.Id.Value);
            purchase.RecordNotificationFailure(ex.Message);
        }

        _coursePurchaseRepository.Update(purchase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
