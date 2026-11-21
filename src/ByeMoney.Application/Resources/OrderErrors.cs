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
    public static string InvalidCartId => Get(nameof(InvalidCartId));
    public static string CartNotFound => Get(nameof(CartNotFound));
    public static string InvalidDeliveryStatus => Get(nameof(InvalidDeliveryStatus));
    public static string InvalidTrackingCode => Get(nameof(InvalidTrackingCode));
    public static string InvalidTrackingProvider => Get(nameof(InvalidTrackingProvider));
    public static string InvalidProviderParcelId => Get(nameof(InvalidProviderParcelId));
    public static string ShipmentProviderUnavailable => Get(nameof(ShipmentProviderUnavailable));
    public static string ShipmentAddressRequired => Get(nameof(ShipmentAddressRequired));
    public static string ShipmentOriginRequired => Get(nameof(ShipmentOriginRequired));
    public static string ShipmentOriginNotConfigured => Get(nameof(ShipmentOriginNotConfigured));
    public static string ShipmentOriginSampleProfile => Get(nameof(ShipmentOriginSampleProfile));
    public static string ShipmentOriginCityMismatch => Get(nameof(ShipmentOriginCityMismatch));
    public static string ShipmentParcelRequired => Get(nameof(ShipmentParcelRequired));
    public static string ShipmentOriginCoordinatesRequired => Get(nameof(ShipmentOriginCoordinatesRequired));
    public static string ShipmentOriginProfileIncomplete => Get(nameof(ShipmentOriginProfileIncomplete));
    public static string ShipmentNotAllowed => Get(nameof(ShipmentNotAllowed));
    public static string ShipmentNoActiveItems => Get(nameof(ShipmentNoActiveItems));
    public static string ShipmentAlreadyDispatched => Get(nameof(ShipmentAlreadyDispatched));
    public static string InvalidBoxTypeId => Get(nameof(InvalidBoxTypeId));
    public static string ShipmentScaledParcelInvalid => Get(nameof(ShipmentScaledParcelInvalid));
    public static string ShipmentCreateFailed => Get(nameof(ShipmentCreateFailed));
    public static string ShipmentCityNotFound => Get(nameof(ShipmentCityNotFound));
    public static string ShipmentCityUnavailable => Get(nameof(ShipmentCityUnavailable));
    public static string ShipmentReadFailed => Get(nameof(ShipmentReadFailed));
    public static string ShipmentPaymentPending => Get(nameof(ShipmentPaymentPending));
    public static string ShipmentPaymentPendingHelp => Get(nameof(ShipmentPaymentPendingHelp));
    public static string ShipmentOutcomeUnknown => Get(nameof(ShipmentOutcomeUnknown));
    public static string ShipmentRequestInProgress => Get(nameof(ShipmentRequestInProgress));
    public static string ShipmentRefreshNotAllowed => Get(nameof(ShipmentRefreshNotAllowed));
    public static string ShipmentStatusUnavailable => Get(nameof(ShipmentStatusUnavailable));
    public static string ShipmentParcelNotFound => Get(nameof(ShipmentParcelNotFound));
    public static string ShipmentQuantityMismatch => Get(nameof(ShipmentQuantityMismatch));
    public static string ShipmentTrackingCodePending => Get(nameof(ShipmentTrackingCodePending));
    public static string ShipmentCollectionTypeRequired => Get(nameof(ShipmentCollectionTypeRequired));
    public static string ShipmentDropOffOfficeRequired => Get(nameof(ShipmentDropOffOfficeRequired));
    public static string PhysicalPurchase_DeliveryBlockedByCarrierReturn => Get(nameof(PhysicalPurchase_DeliveryBlockedByCarrierReturn));
    public static string CarrierReturn_BuyerConfirmationBlocked => Get(nameof(CarrierReturn_BuyerConfirmationBlocked));
    public static string CarrierReturn_InvalidResolution => Get(nameof(CarrierReturn_InvalidResolution));
    public static string CarrierReturn_NoteRequired => Get(nameof(CarrierReturn_NoteRequired));
    public static string CarrierReturn_NoteTooLong => Get(nameof(CarrierReturn_NoteTooLong));
    public static string CarrierReturn_NotReturned => Get(nameof(CarrierReturn_NotReturned));
    public static string CarrierReturn_ResolutionConflict => Get(nameof(CarrierReturn_ResolutionConflict));
    public static string CarrierAlert_InvalidResolution => Get(nameof(CarrierAlert_InvalidResolution));
    public static string CarrierAlert_NoteRequired => Get(nameof(CarrierAlert_NoteRequired));
    public static string CarrierAlert_NoteTooLong => Get(nameof(CarrierAlert_NoteTooLong));
    public static string CarrierAlert_NotAlerted => Get(nameof(CarrierAlert_NotAlerted));
    public static string CarrierAlert_ResolutionConflict => Get(nameof(CarrierAlert_ResolutionConflict));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
