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
    public static string InvalidTrackingCode => Get(nameof(InvalidTrackingCode));
    public static string InvalidTrackingProvider => Get(nameof(InvalidTrackingProvider));
    public static string InvalidProviderParcelId => Get(nameof(InvalidProviderParcelId));
    public static string ShipmentProviderUnavailable => Get(nameof(ShipmentProviderUnavailable));
    public static string ShipmentAddressRequired => Get(nameof(ShipmentAddressRequired));
    public static string ShipmentOriginRequired => Get(nameof(ShipmentOriginRequired));
    public static string ShipmentNotAllowed => Get(nameof(ShipmentNotAllowed));
    public static string ShipmentCreateFailed => Get(nameof(ShipmentCreateFailed));
    public static string ShipmentCityNotFound => Get(nameof(ShipmentCityNotFound));
    public static string ShipmentCityUnavailable => Get(nameof(ShipmentCityUnavailable));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
