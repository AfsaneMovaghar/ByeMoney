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
    public decimal LineTotalInNoor => Quantity * PriceInNoor;
}
