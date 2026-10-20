

namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CourseCartQuoteItem(string ExternalCourseId, string Title, decimal BasePriceInNoor,
    decimal ItemDiscountInNoor, decimal CouponDiscountInNoor, decimal PriceInNoor);
