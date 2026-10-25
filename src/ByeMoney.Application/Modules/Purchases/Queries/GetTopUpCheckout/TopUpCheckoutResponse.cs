namespace ByeMoney.Application.Modules.Purchases.Queries.GetTopUpCheckout;

public sealed record TopUpCheckoutResponse(
    Guid TopUpId,
    string TopUpStatus,
    IReadOnlyList<CheckoutCourseResponse> Courses,
    Guid? CartId = null,
    bool IsUnderReview = false,
    string? ReviewCaseId = null,
    string? ReviewReasonCode = null,
    string? GatewayResultKind = null);

