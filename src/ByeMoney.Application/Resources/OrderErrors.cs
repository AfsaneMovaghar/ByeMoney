using System.Resources;

namespace ByeMoney.Application.Resources;

public static class OrderErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.OrderErrors", typeof(OrderErrors).Assembly);

    public static string NotFound => Get(nameof(NotFound));
    public static string InvalidOrderId => Get(nameof(InvalidOrderId));
    public static string InvalidSourceType => Get(nameof(InvalidSourceType));
    public static string PhysicalPurchaseNotFound => Get(nameof(PhysicalPurchaseNotFound));
    public static string InvalidPurchaseId => Get(nameof(InvalidPurchaseId));
    public static string InvalidDeliveryStatus => Get(nameof(InvalidDeliveryStatus));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
