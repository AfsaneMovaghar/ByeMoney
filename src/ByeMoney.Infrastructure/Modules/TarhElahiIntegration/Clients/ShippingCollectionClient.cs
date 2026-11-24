using System.Net.Http.Json;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

/// <summary>
/// <c>GET /api/integrations/byemoney/v1/shipping/collection-type</c> با کلید سرویس.
/// </summary>
public sealed class ShippingCollectionClient(HttpClient http) : IShippingCollectionReader
{
    public async Task<ShippingCollectionRead> ReadAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(
                "api/integrations/byemoney/v1/shipping/collection-type", ct);
            if (!response.IsSuccessStatusCode)
            {
                return ShippingCollectionRead.Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<CollectionBody>(ct);
            return ShippingCollectionTypes.IsAllowed(payload?.CollectionType, out var normalized)
                ? ShippingCollectionRead.Found(
                    normalized,
                    ShippingDispatchCodes.NormalizeCourier(payload?.CourierCode),
                    ShippingDispatchCodes.NormalizeService(payload?.ServiceType))
                : ShippingCollectionRead.Unavailable();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return ShippingCollectionRead.Unavailable();
        }
    }

    private sealed record CollectionBody(string? CollectionType, string? CourierCode, string? ServiceType);
}
