using System.Text.Json.Serialization;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Contracts;

/// <summary>
/// بدنهٔ سیمی <c>carts/prepare</c> طرح الهی (I09-C). نام <c>externalCourseIds</c> قرارداد همین فروشنده است
/// و فقط در این آداپتور به نام عام قلم نگاشت می‌شود.
/// </summary>
internal sealed record TarhElahiCartPrepareRequest(
    Guid CartId,
    string BuyerExternalUserId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? ExternalCourseIds,
    string? CouponCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<TarhElahiCartPrepareItemRequest>? Items = null);

internal sealed record TarhElahiCartPrepareItemRequest(
    string ExternalId,
    string Type,
    int Quantity);

internal sealed record TarhElahiCartPrepareResponse(
    Guid CartId,
    string BuyerExternalUserId,
    string? CouponCode,
    IReadOnlyList<TarhElahiCartPrepareResponseItem>? Items,
    decimal OriginalTotalInNoor = 0m,
    decimal ItemDiscountInNoor = 0m,
    decimal CouponDiscountInNoor = 0m,
    decimal ShippingAmountInNoor = 0m,
    string? ShippingRuleLabel = null,
    decimal PriceInNoor = 0m,
    TarhElahiCartAddressSnapshot? AddressSnapshot = null,
    string? AddressVersion = null,
    string? ShippingOriginId = null,
    string? ShippingOriginLabel = null);

/// <summary>
/// آدرس تحویل در پاسخ prepare (I09-PH). <c>postalCode</c> اختیاری است.
/// <c>CityDocumentId</c> شناسهٔ ردیف شهر است و کد حامل نیست.
/// </summary>
internal sealed record TarhElahiCartAddressSnapshot(
    string? Province,
    string? City,
    string? PostalCode,
    string? FullAddress,
    string? RecipientName,
    string? RecipientPhone,
    string? CityDocumentId = null);

/// <summary>
/// بدنهٔ خطای سبد طرح الهی؛ <c>lineError</c> فقط وقتی می‌آید که یک قلم مشخص قابل فروش نباشد.
/// </summary>
internal sealed record TarhElahiCartErrorResponse(
    string? Code,
    string? Error,
    TarhElahiCartLineError? LineError);

internal sealed record TarhElahiCartLineError(
    string? ExternalId,
    string? Type,
    int? Quantity,
    string? Reason,
    int? AvailableQuantity);

internal sealed record TarhElahiCartPrepareResponseItem(
    string ExternalCourseId,
    string Title,
    decimal BasePriceInNoor,
    decimal ItemDiscountInNoor,
    decimal CouponDiscountInNoor,
    decimal PriceInNoor,
    ProductItemType Type = ProductItemType.Course,
    string? ParentExternalId = null,
    int Quantity = 1,
    TarhElahiParcelFacts? Parcel = null);

/// <summary>
/// عوامل مرسولهٔ همین ردیف کالا در prepare؛ وزن گرم کل ردیف (واحد×تعداد) و ابعاد سانتی‌متر.
/// فیلدها nullableاند تا نبود یا ناقص‌بودن داده در بای‌مانی به عدد جایگزین تبدیل نشود.
/// </summary>
internal sealed record TarhElahiParcelFacts(
    int? WeightGrams,
    int? LengthCm,
    int? WidthCm,
    int? HeightCm,
    long? DeclaredValueIrr,
    bool? IsFragile);
