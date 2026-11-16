using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByeMoney.Domain.Common;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

/// <summary>
/// آداپتور قیمت‌گذاری و رزرو سبد طرح الهی. ترجمهٔ نام عام قلم به قرارداد فروشنده و قواعد مخصوص
/// کاتالوگ طرح الهی (والد فصل، تعداد دوره) فقط اینجا نگهداری می‌شوند.
/// </summary>
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
            using var response = await http.PostAsJsonAsync(
                "api/integrations/byemoney/v1/carts/prepare",
                ToWireRequest(request),
                ct);
            if (!response.IsSuccessStatusCode)
            {
                return await FailureAsync<CartQuote>(response, ct);
            }

            var wire = await response.Content.ReadFromJsonAsync<TarhElahiCartPrepareResponse>(QuoteJsonOptions, ct);
            var quote = wire is null ? null : ToQuote(wire);
            return quote is null
                ? Result<CartQuote>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid)
                : Result<CartQuote>.Success(quote);
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
            return response.IsSuccessStatusCode ? Result.Success() : await FailureAsync<bool>(response, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        { return Result.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable); }
    }

    /// <summary>
    /// سبد فقط دوره/فصل تک‌تایی با قالب قدیمی <c>externalCourseIds</c> فرستاده می‌شود تا قرارداد موجود استرپی
    /// دست نخورد؛ کالا یا تعداد بیش از یک به قالب اقلام تایپ‌دار نیاز دارد.
    /// </summary>
    internal static TarhElahiCartPrepareRequest ToWireRequest(CartQuoteRequest request)
    {
        var needsTypedItems = request.Items.Any(x => x.Type == ProductItemType.Product || x.Quantity > 1);
        if (!needsTypedItems)
        {
            var ids = request.Items
                .Select(x => x.ExternalId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return new TarhElahiCartPrepareRequest(request.CartId, request.BuyerExternalUserId, ids, request.CouponCode);
        }

        var items = request.Items
            .Select(x => new TarhElahiCartPrepareItemRequest(x.ExternalId, ToWireType(x.Type), x.Quantity))
            .ToArray();
        return new TarhElahiCartPrepareRequest(request.CartId, request.BuyerExternalUserId, null, request.CouponCode, items);
    }

    private static string ToWireType(ProductItemType type) => type switch
    {
        ProductItemType.CourseChapter => "course_chapter",
        ProductItemType.Product => "product",
        _ => "course"
    };

    private static CartQuote? ToQuote(TarhElahiCartPrepareResponse wire)
    {
        if (wire.Items is null ||
            wire.Items.Any(x => string.IsNullOrWhiteSpace(x.ExternalCourseId)) ||
            !wire.Items.All(HasValidCatalogShape))
        {
            return null;
        }

        var items = wire.Items
            .Select(x => new CartQuoteItem(
                x.ExternalCourseId,
                x.Title,
                x.BasePriceInNoor,
                x.ItemDiscountInNoor,
                x.CouponDiscountInNoor,
                x.PriceInNoor,
                x.Type,
                x.ParentExternalId,
                x.Quantity))
            .ToArray();

        var hasProduct = items.Any(x => x.Type == ProductItemType.Product);
        var address = hasProduct ? ToAddress(wire) : null;
        var originId = string.IsNullOrWhiteSpace(wire.ShippingOriginId)
            ? null
            : wire.ShippingOriginId.Trim();
        if (hasProduct && (address is null || originId is null || originId.Length > Cart.ShippingOriginIdMaxLength))
        {
            return null;
        }

        return new CartQuote(
            wire.CartId,
            wire.BuyerExternalUserId,
            wire.CouponCode,
            items,
            wire.OriginalTotalInNoor,
            wire.ItemDiscountInNoor,
            wire.CouponDiscountInNoor,
            wire.ShippingAmountInNoor,
            wire.ShippingRuleLabel,
            wire.PriceInNoor,
            address,
            hasProduct ? originId : null);
    }

    /// <summary>
    /// سبد دارای کالا باید آدرس کامل و نسخهٔ معتبر داشته باشد؛ در غیر این صورت پاسخ نامعتبر است.
    /// نبود <c>postalCode</c> مجاز است.
    /// </summary>
    private static ShippingAddress? ToAddress(TarhElahiCartPrepareResponse wire)
    {
        var snapshot = wire.AddressSnapshot;
        if (snapshot is null)
        {
            return null;
        }

        var postalCode = string.IsNullOrWhiteSpace(snapshot.PostalCode) ? null : snapshot.PostalCode.Trim();
        var address = new ShippingAddress(
            wire.AddressVersion?.Trim() ?? string.Empty,
            snapshot.Province?.Trim() ?? string.Empty,
            snapshot.City?.Trim() ?? string.Empty,
            postalCode,
            snapshot.FullAddress?.Trim() ?? string.Empty,
            snapshot.RecipientName?.Trim() ?? string.Empty,
            snapshot.RecipientPhone?.Trim() ?? string.Empty);
        return address.IsValid() ? address : null;
    }

    /// <summary>
    /// قاعدهٔ کاتالوگ طرح الهی: فصل والد دارد و تک‌تایی است، دوره والد ندارد و تک‌تایی است، کالا والد ندارد.
    /// </summary>
    private static bool HasValidCatalogShape(TarhElahiCartPrepareResponseItem item) => item.Type switch
    {
        ProductItemType.CourseChapter => !string.IsNullOrWhiteSpace(item.ParentExternalId) && item.Quantity == 1,
        ProductItemType.Course => item.ParentExternalId is null && item.Quantity == 1,
        ProductItemType.Product => item.ParentExternalId is null && item.Quantity >= 1,
        _ => false
    };

    private static async Task<Result<T>> FailureAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var lineError = await ReadStockLineErrorAsync(response, ct);
            if (lineError is not null)
            {
                return CartStockFailure<T>.Create(lineError);
            }
        }

        return Failure<T>(response.StatusCode);
    }

    /// <summary>
    /// فقط کمبود موجودی قلم به خطای اختصاصی نگاشت می‌شود؛ بدنهٔ ناقص یا دلیل دیگر همان تعارض عمومی می‌ماند.
    /// </summary>
    private static async Task<CartLineError?> ReadStockLineErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        TarhElahiCartErrorResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<TarhElahiCartErrorResponse>(ct);
        }
        catch (JsonException)
        {
            return null;
        }

        var line = body?.LineError;
        if (line is null || string.IsNullOrWhiteSpace(line.ExternalId) || line.AvailableQuantity < 0)
        {
            return null;
        }

        var isStockReason = line.Reason == CartStockReasons.OutOfStock ||
            (line.Reason == CartStockReasons.InsufficientStock && line.AvailableQuantity is not null);
        return isStockReason
            ? new CartLineError(line.ExternalId, line.Reason!, line.AvailableQuantity)
            : null;
    }

    private static Result<T> Failure<T>(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => Result<T>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid),
        HttpStatusCode.Conflict or HttpStatusCode.NotFound => Result<T>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict),
        _ => Result<T>.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable)
    };
}
