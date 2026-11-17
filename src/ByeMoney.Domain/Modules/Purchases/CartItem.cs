namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CartItem(
    string ExternalItemId,
    string Title,
    decimal PriceInNoor,
    decimal AllocatedAmountInNoor,
    ProductItemType ItemType = ProductItemType.Course,
    string? ParentExternalId = null,
    int Quantity = 1)
{
    /// <summary>عوامل مرسولهٔ فریزشدهٔ همین قلم؛ برای دوره/فصل و دادهٔ ناقص null است.</summary>
    public ParcelFacts? Parcel { get; init; }

    public decimal LineTotalInNoor => Quantity * PriceInNoor;
}
