namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CourseCartPurchaseAllocation(
    string ExternalCourseId,
    decimal AmountInNoor,
    ProductItemType ItemType = ProductItemType.Course);

