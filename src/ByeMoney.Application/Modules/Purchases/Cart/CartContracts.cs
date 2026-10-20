using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

namespace ByeMoney.Application.Modules.Purchases.Cart;

public static class CartErrorCodes
{
    public const string Invalid = "CART_INVALID";
    public const string Conflict = "CART_CONFLICT";
    public const string NotFound = "CART_NOT_FOUND";
    public const string InsufficientBalance = "CART_INSUFFICIENT_BALANCE";
    public const string Unavailable = "CART_UNAVAILABLE";
}
public static class CartErrors
{
    private static string Read(string key) => Resources.ApplicationErrors.ResourceManager.GetString(key)!;
    public static string Invalid => Read("Cart_Invalid");
    public static string Conflict => Read("Cart_Conflict");
    public static string NotFound => Read("Cart_NotFound");
    public static string InsufficientBalance => Read("Cart_InsufficientBalance");
    public static string Unavailable => Read("Cart_Unavailable");
}
public sealed record CartCourseResponse(string ExternalCourseId, string Title, decimal PriceInNoor, string PurchaseStatus);
public sealed record CartResponse(Guid CartId, string Status, string? CouponCode, decimal OriginalTotalInNoor,
    decimal ItemDiscountInNoor, decimal CouponDiscountInNoor, decimal PriceInNoor, decimal BalanceInNoor,
    decimal ShortfallInNoor, decimal AmountRial, DateTime ExpiresAtUtc, Guid? TopUpRequestId, string? TopUpStatus,
    IReadOnlyList<CartCourseResponse> Courses);
public sealed record CartQuote(Guid CartId, string BuyerExternalUserId, string? CouponCode, IReadOnlyList<CourseCartQuoteItem> Items);
public sealed record CartQuoteRequest(Guid CartId, string BuyerExternalUserId, string[] ExternalCourseIds, string? CouponCode);
public sealed record CartTransitionRequest(Guid CartId, string BuyerExternalUserId, IReadOnlyList<Guid>? PurchaseIds = null);

public interface ICartPricingClient
{
    Task<Result<CartQuote>> PrepareAsync(CartQuoteRequest request, CancellationToken ct);
    Task<Result> TransitionAsync(CartTransitionRequest request, string action, CancellationToken ct);
}
public interface ICourseCartStore
{
    Task<CourseCart?> FindAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<CourseCart?> FindByTopUpAsync(Guid topUpId, Guid buyerId, CancellationToken ct);
    Task<Result<CourseCart>> CreateAsync(CourseCart cart, CancellationToken ct);
    Task<Result<CourseCart>> CancelAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<Result<CourseCart>> ReopenAsync(Guid id, Guid buyerId, Guid reservationId, CancellationToken ct);
    Task<CourseCart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CourseCartQuoteItem> items, Guid reservationId, CancellationToken ct);
    Task<Result<CourseCart>> PayAsync(
        Guid id,
        Guid buyerId,
        Guid reservationId,
        CancellationToken ct,
        decimal? referenceRate = null);
    Task<Result<CreateTopUpRequestResponse>> CreateTopUpAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task FailAsync(Guid id, Guid buyerId, bool expired, Guid reservationId, CancellationToken ct);
    Task RecordSynchronizationAsync(Guid id, Guid buyerId, bool succeeded, string? errorCode, Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<CourseCart>> GetPendingAsync(CancellationToken ct);
}
