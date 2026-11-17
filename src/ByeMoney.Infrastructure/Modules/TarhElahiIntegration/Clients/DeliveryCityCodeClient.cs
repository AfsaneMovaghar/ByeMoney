using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

/// <summary>
/// <c>POST /api/integrations/byemoney/v1/localities/carrier-code</c> با کلید سرویس.
/// بدنه فقط شناسهٔ شهر است، یا برای سفارش قدیمی فقط نام دقیق استان و شهر.
/// </summary>
public sealed class DeliveryCityCodeClient(HttpClient http) : IDeliveryCityCodeReader
{
    public async Task<DeliveryCityCodeRead> ReadAsync(
        string? cityDocumentId, string province, string city, CancellationToken ct)
    {
        var documentId = string.IsNullOrWhiteSpace(cityDocumentId) ? null : cityDocumentId.Trim();
        if (documentId is null &&
            (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(city)))
        {
            return DeliveryCityCodeRead.NotFound();
        }

        object body = documentId is not null
            ? new CityDocumentBody(documentId)
            : new CityNameBody(province.Trim(), city.Trim());

        try
        {
            using var response = await http.PostAsJsonAsync(
                "api/integrations/byemoney/v1/localities/carrier-code", body, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return DeliveryCityCodeRead.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return DeliveryCityCodeRead.Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<CarrierCityCodeBody>(ct);
            return payload is { CarrierCityCode: > 0 }
                ? DeliveryCityCodeRead.Found(payload.CarrierCityCode)
                : DeliveryCityCodeRead.NotFound();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return DeliveryCityCodeRead.Unavailable();
        }
    }

    private sealed record CityDocumentBody(string CityDocumentId);

    private sealed record CityNameBody(string Province, string City);

    private sealed record CarrierCityCodeBody(int CarrierCityCode);
}
