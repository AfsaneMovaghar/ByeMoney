namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CartItem(
    string ExternalCourseId,
    string Title,
    decimal PriceInNoor,
    decimal AllocatedAmountInNoor,
    ProductItemType ItemType = ProductItemType.Course,
    string? ParentExternalId = null,
    int Quantity = 1,
    decimal? RegularAmountInNoor = null,
    decimal? GiftAmountInNoor = null)
{
    public decimal LineTotalInNoor => Quantity * PriceInNoor;
}
