using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class ProductSnapshot
{
    /// <summary>Origin catalog system identifier, e.g. "tarh_elahi".</summary>
    public string ProductSource { get; private set; } = null!;

    /// <summary>External product id, e.g. Strapi Course documentId.</summary>
    public string ExternalProductId { get; private set; } = null!;

    /// <summary>Authoritative title of the product at purchase time.</summary>
    public string ProductTitle { get; private set; } = null!;

    /// <summary>Authoritative Rial price from catalog at purchase time.</summary>
    public decimal PriceInRialAtPurchaseTime { get; private set; }

    /// <summary>Rial to Noor conversion rate applied at purchase time.</summary>
    public decimal ConversionRateAtPurchaseTime { get; private set; }

    /// <summary>Final calculated Noor price at full decimal(18,4) precision.</summary>
    public decimal PriceInNoorAtPurchaseTime { get; private set; }

    /// <summary>Timestamp when the snapshot was frozen and purchase initiated (UTC).</summary>
    public DateTime PurchasedAt { get; private set; }

    private ProductSnapshot() { }

    public static ProductSnapshot Create(
        string externalProductId,
        string productTitle,
        decimal priceRial,
        decimal conversionRate,
        decimal priceNoor,
        string productSource = ProductCatalogSources.TarhElahi)
    {
        if (string.IsNullOrWhiteSpace(externalProductId))
            throw new DomainException(DomainErrors.ProductSnapshot_ExternalProductIdRequired);

        if (string.IsNullOrWhiteSpace(productTitle))
            throw new DomainException(DomainErrors.ProductSnapshot_ProductTitleRequired);

        if (priceRial <= 0)
            throw new DomainException(DomainErrors.CoursePurchase_InvalidPrice);

        if (conversionRate <= 0)
            throw new DomainException(DomainErrors.CoursePurchase_InvalidConversionRate);

        if (priceNoor <= 0)
            throw new DomainException(DomainErrors.CoursePurchase_InvalidPrice);

        return new ProductSnapshot
        {
            ProductSource = productSource,
            ExternalProductId = externalProductId.Trim(),
            ProductTitle = productTitle.Trim(),
            PriceInRialAtPurchaseTime = priceRial,
            ConversionRateAtPurchaseTime = conversionRate,
            PriceInNoorAtPurchaseTime = priceNoor,
            PurchasedAt = DateTime.UtcNow
        };
    }
}

