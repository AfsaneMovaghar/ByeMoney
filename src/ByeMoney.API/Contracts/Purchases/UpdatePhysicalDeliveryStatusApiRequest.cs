namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// <c>trackingCode</c> کد رهگیری مرسوله است (اختیاری)، <c>trackingProvider</c> منبع آن است و
/// <c>providerParcelId</c> شناسهٔ ارجاع مرسوله نزد همان واسط.
/// </summary>
public record UpdatePhysicalDeliveryStatusApiRequest(
    string Status, string? TrackingCode = null, string? TrackingProvider = null, string? ProviderParcelId = null);
