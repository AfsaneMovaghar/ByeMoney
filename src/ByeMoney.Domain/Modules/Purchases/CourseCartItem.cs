namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CourseCartItem(
    string ExternalCourseId,
    string Title,
    decimal PriceInNoor,
    ProductItemType ItemType = ProductItemType.Course,
    string? ParentExternalId = null,
    int Quantity = 1)
{
    public decimal LineTotalInNoor => Quantity * PriceInNoor;
}

