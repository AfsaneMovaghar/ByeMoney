namespace ByeMoney.Application.Modules.Purchases.Queries.GetTopUpCheckout;

public sealed record TopUpCheckoutResponse(
    Guid TopUpId, string TopUpStatus, IReadOnlyList<CheckoutCourseResponse> Courses, Guid? CartId = null);
