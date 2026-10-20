using ByeMoney.Application.Modules.Purchases.Queries.History;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Queries.Orders;

public enum CourseOrderSourceType
{
    Cart,
    TopUpCheckout,
    Purchase
}

public enum CourseOrderFinancialStatus
{
    Preparing,
    AwaitingPayment,
    Processing,
    Paid,
    Failed,
    Expired,
    RequiresReview
}

public enum CourseOrderDeliveryStatus
{
    NotStarted,
    Pending,
    PartiallyDelivered,
    Delivered
}

public sealed record CourseOrderFilter(
    int Page = 1, int PageSize = 20, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null, string? SourceType = null);

public sealed record AdminCourseOrderFilter(
    int Page = 1, int PageSize = 20, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null, string? SourceType = null,
    string? BuyerExternalUserId = null)
{
    public CourseOrderFilter ToFilter() => new(
        Page, PageSize, FromUtc, ToUtc, FinancialStatus, DeliveryStatus, SourceType);
}

public sealed record CourseOrderListItem(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount);

public sealed record AdminCourseOrderListItem(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    string BuyerExternalUserId, string? BuyerDisplayName);

public sealed record CourseOrderItem(
    string ExternalCourseId, string? Title, decimal? PriceInNoor, decimal? FinalAmountInNoor,
    Guid? PurchaseId, Guid? TransactionId, DateTime? PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus);

public sealed record CourseOrderTopUp(
    Guid TopUpRequestId, string Status, decimal AmountNoor, decimal? AmountRial,
    decimal? RialPerNoorSnapshot, string ClientReferenceCode, DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc, string? GatewayResultKind, string? ReviewReasonCode);

public sealed record CourseOrderDetails(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    string? CartStatus, DateTime? ExpiresAtUtc, Guid? CurrentTopUpRequestId,
    PurchaseCartSummary? CartSummary, IReadOnlyList<CourseOrderItem> Items,
    IReadOnlyList<CourseOrderTopUp> TopUps)
{
    public CourseOrderListItem ToListItem() => new(
        OrderId, SourceType, CartId, CreatedAtUtc, UpdatedAtUtc, FinancialStatus,
        DeliveryStatus, FinalAmountInNoor, ItemCount);
}

public sealed record CourseOrderBuyer(Guid Id, string ExternalUserId, string? DisplayName);

public sealed record CourseOrderSnapshot(
    IReadOnlyList<CourseCart> Carts, IReadOnlyList<TopUpRequest> TopUps,
    IReadOnlyList<CoursePurchase> Purchases, IReadOnlyList<CourseOrderBuyer> Buyers);

public interface ICourseOrderReader
{
    Task<CourseOrderSnapshot> ReadAsync(Guid? buyerId, string? buyerExternalUserId, CancellationToken ct);
}

public sealed record CourseOrderReadRow(Guid BuyerId, CourseOrderDetails Details);

public static class CourseOrderId
{
    public static string For(CourseOrderSourceType source, Guid id) =>
        $"{Prefix(source)}:{id:D}";

    public static bool IsValid(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var parts = value.Split(':');
        return parts.Length == 2 && parts[0] is "cart" or "topup" or "purchase" &&
            Guid.TryParseExact(parts[1], "D", out var id) && id != Guid.Empty &&
            parts[1] == id.ToString("D");
    }

    private static string Prefix(CourseOrderSourceType source) => source switch
    {
        CourseOrderSourceType.Cart => "cart",
        CourseOrderSourceType.TopUpCheckout => "topup",
        CourseOrderSourceType.Purchase => "purchase",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
