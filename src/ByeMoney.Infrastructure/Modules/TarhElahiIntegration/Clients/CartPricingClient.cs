using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Interfaces;
using ByeMoney.Domain.Common.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByeMoney.Domain.Common;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

public sealed class CartPricingClient(HttpClient http) : ICartPricingClient
{
    private static readonly JsonSerializerOptions QuoteJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) }
    };
    public async Task<Result<CartQuote>> PrepareAsync(CartQuoteRequest request, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/integrations/byemoney/v1/carts/prepare", request, ct);
            if (!response.IsSuccessStatusCode) return Failure<CartQuote>(response.StatusCode);
            var quote = await response.Content.ReadFromJsonAsync<CartQuote>(QuoteJsonOptions, ct);
            return quote is null ? Result<CartQuote>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid) : Result<CartQuote>.Success(quote);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        { return Result<CartQuote>.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable); }
    }
    public async Task<Result> TransitionAsync(CartTransitionRequest request, string action, CancellationToken ct)
    {
        if (action is not ("check" or "commit" or "release")) throw new ArgumentOutOfRangeException(nameof(action));
        try
        {
            using var response = await http.PostAsJsonAsync($"api/integrations/byemoney/v1/carts/{request.CartId:D}/{action}", request, ct);
            return response.IsSuccessStatusCode ? Result.Success() : Failure<bool>(response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        { return Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable); }
    }
    private static Result<T> Failure<T>(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => Result<T>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid),
        HttpStatusCode.Conflict or HttpStatusCode.NotFound => Result<T>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict),
        _ => Result<T>.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable)
    };
}
