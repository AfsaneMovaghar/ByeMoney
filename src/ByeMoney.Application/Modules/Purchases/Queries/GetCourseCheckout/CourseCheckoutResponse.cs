namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCheckout;

public sealed record CourseCheckoutResponse(
    string ExternalCourseId, decimal PriceInNoor, decimal BalanceInNoor,
    decimal ShortfallInNoor, decimal AmountRial, string PurchaseStatus);
