using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Services;

public static class PurchaseNotificationItem
{
    public static TarhElahiNotificationItemDto FromSnapshot(ProductSnapshot snapshot) => new()
    {
        Type = snapshot.ItemType switch
        {
            ProductItemType.Course => TarhElahiCatalogItemType.Course,
            ProductItemType.CourseChapter => TarhElahiCatalogItemType.CourseChapter,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot))
        },
        ExternalId = snapshot.ExternalProductId,
        ParentExternalId = snapshot.ExternalParentId
    };
}
