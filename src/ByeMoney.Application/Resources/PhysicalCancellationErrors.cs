using System.Resources;

namespace ByeMoney.Application.Resources;

public static class PhysicalCancellationErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.PhysicalCancellationErrors", typeof(PhysicalCancellationErrors).Assembly);

    public static string NotFound => Get(nameof(NotFound));
    public static string RestockOperationNotFound => Get(nameof(RestockOperationNotFound));
    public static string NoPhysicalItems => Get(nameof(NoPhysicalItems));
    public static string Conflict => Get(nameof(Conflict));
    public static string AwaitingTopUp => Get(nameof(AwaitingTopUp));
    public static string AlreadyShipped => Get(nameof(AlreadyShipped));
    public static string ManualReview => Get(nameof(ManualReview));
    public static string Unavailable => Get(nameof(Unavailable));
    public static string InvalidCartId => Get(nameof(InvalidCartId));
    public static string InvalidPurchaseId => Get(nameof(InvalidPurchaseId));
    public static string InvalidOperationId => Get(nameof(InvalidOperationId));
    public static string InvalidQuantity => Get(nameof(InvalidQuantity));
    public static string InvalidReason => Get(nameof(InvalidReason));
    public static string WarningCouponRemoved => Get("Warning_CouponRemoved");
    public static string WarningFreeShippingRemoved => Get("Warning_FreeShippingRemoved");
    public static string WarningShippingNotRefunded => Get("Warning_ShippingNotRefunded");

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
