namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CourseCartQuoteItem(
    string ExternalCourseId,
    string Title,
    decimal BasePriceInNoor,
    decimal ItemDiscountInNoor,
    decimal CouponDiscountInNoor,
    decimal PriceInNoor,
    ProductItemType Type = ProductItemType.Course,
    string? ParentExternalId = null,
    int Quantity = 1)
{
    public decimal LineTotalInNoor => Quantity * BasePriceInNoor;
}
