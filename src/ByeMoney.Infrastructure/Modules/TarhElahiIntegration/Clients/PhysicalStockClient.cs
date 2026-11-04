using System.Net;
using System.Net.Http.Json;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Domain.Common;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

/// <summary>
/// فراخوانی <c>POST carts/{cartId}/restock</c> استرپی (I09-PC) با کلید سرویس. خطای HTTP یا قطعی به Result تبدیل
/// می‌شود تا فراخواننده عملیات را در صف تلاش مجدد نگه دارد.
/// </summary>
public sealed class PhysicalStockClient(HttpClient http) : IPhysicalStockClient
{
    public async Task<Result> RestockAsync(PhysicalRestockRequest request, CancellationToken ct)
    {
        var body = new RestockBody(
            request.OperationId,
            request.Items.Select(x => new RestockItemBody(x.PurchaseId, x.Quantity)).ToArray());

        try
        {
            using var response = await http.PostAsJsonAsync(
                $"api/integrations/byemoney/v1/carts/{request.StrapiCartId:D}/restock", body, ct);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Failure(response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable);
        }
    }

    private static Result Failure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => Result.Failure(CartErrors.Invalid, CartErrorCodes.Invalid),
        HttpStatusCode.Conflict or HttpStatusCode.NotFound => Result.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict),
        _ => Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable)
    };

    private sealed record RestockBody(Guid OperationId, IReadOnlyList<RestockItemBody> Items);

    private sealed record RestockItemBody(Guid PurchaseId, int Quantity);
}
