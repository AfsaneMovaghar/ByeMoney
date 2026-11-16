using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;

/// <summary>
/// استعلام وضعیت مرسوله از پستکس با هدر <c>x-api-key</c> (روش احراز هویت پستکس؛ در مستندات OpenAPI
/// تعریف نشده و از پلاگین رسمی ووکامرس همان شرکت تأیید شد). بدون کلید یا با <c>Enabled=false</c>
/// هیچ درخواستی ارسال نمی‌شود؛ شناسهٔ ارجاع مرسوله برای استعلام اجباری است.
/// </summary>
public sealed class PostexTrackingClient(HttpClient http, PostexOptions options) : ITrackingProviderClient
{
    public string Provider => PhysicalProductPurchase.PostexProvider;

    public bool IsConfigured => options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<TrackingResult> GetTrackingAsync(TrackingLookup lookup, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(lookup.ProviderReference))
        {
            return new TrackingResult(TrackingOutcome.NotConfigured);
        }

        var path = $"api/v1/parcels/{Uri.EscapeDataString(lookup.ProviderReference.Trim())}/status";

        try
        {
            using var response = await http.GetAsync(path, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return PostexStatusParser.Parse(body, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return new TrackingResult(TrackingOutcome.Failed);
        }
    }
}
