using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class ProductSnapshot
{
    public ProductItemType ItemType { get; private set; }

    public string? ExternalParentId { get; private set; }

    /// <summary>Origin catalog system identifier, e.g. "tarh_elahi".</summary>
    public string ProductSource { get; private set; } = null!;

    /// <summary>External product id, e.g. Strapi Course documentId.</summary>
    public string ExternalProductId { get; private set; } = null!;

    /// <summary>Authoritative title of the product at purchase time.</summary>
    public string ProductTitle { get; private set; } = null!;

    /// <summary>Historical Rial price, retained only for purchases made before Noor pricing.</summary>
    public decimal? PriceInRialAtPurchaseTime { get; private set; }

    /// <summary>Historical conversion rate, retained only for old purchases.</summary>
    public decimal? ConversionRateAtPurchaseTime { get; private set; }

    /// <summary>نرخ مرجع ریال به‌ازای یک نور هنگام خرید؛ صرفاً اطلاعاتی و مستقل از محاسبات مالی.</summary>
    public decimal? ReferenceRialPerNoorAtPurchaseTime { get; private set; }

    /// <summary>Final calculated Noor price at full decimal(18,4) precision.</summary>
    public decimal PriceInNoorAtPurchaseTime { get; private set; }

    /// <summary>Timestamp when the snapshot was frozen and purchase initiated (UTC).</summary>
    public DateTime PurchasedAt { get; private set; }

    private ProductSnapshot() { }

    public static ProductSnapshot Create(
        string externalProductId,
        string productTitle,
        decimal priceNoor,
        string productSource = ProductCatalogSources.TarhElahi,
        bool isFree = false,
        decimal? referenceRialPerNoorAtPurchaseTime = null,
        ProductItemType itemType = ProductItemType.Course,
        string? externalParentId = null)
    {
        if (string.IsNullOrWhiteSpace(externalProductId))
            throw new DomainException(DomainErrors.ProductSnapshot_ExternalProductIdRequired);

        if (string.IsNullOrWhiteSpace(productTitle))
            throw new DomainException(DomainErrors.ProductSnapshot_ProductTitleRequired);

        if (priceNoor < 0 || (!isFree && priceNoor == 0))
            throw new DomainException(DomainErrors.CoursePurchase_InvalidPrice);

        if (!Enum.IsDefined(itemType) ||
            (itemType == ProductItemType.CourseChapter
                ? string.IsNullOrWhiteSpace(externalParentId) || externalParentId.Length > 100 ||
                  externalParentId == externalProductId
                : externalParentId is not null))
        {
            throw new DomainException(DomainErrors.ProductSnapshot_InvalidItemTypeOrParent);
        }

        return new ProductSnapshot
        {
            ItemType = itemType,
            ExternalParentId = externalParentId,
            ProductSource = productSource,
            ExternalProductId = externalProductId.Trim(),
            ProductTitle = productTitle.Trim(),
            PriceInNoorAtPurchaseTime = priceNoor,
            ReferenceRialPerNoorAtPurchaseTime = referenceRialPerNoorAtPurchaseTime,
            PurchasedAt = DateTime.UtcNow
        };
    }
}

