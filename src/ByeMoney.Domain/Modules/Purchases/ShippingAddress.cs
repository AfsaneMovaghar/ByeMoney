namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// نسخهٔ ثابت آدرس تحویل کالا که استرپی هنگام قیمت‌گذاری سبد برمی‌گرداند (I09-PH).
/// <c>Version</c> هش آدرس در همان لحظه است و فقط برای تشخیص تغییر آدرس به کار می‌رود.
/// <c>CityDocumentId</c> شناسهٔ ردیف شهر خودمان است، نه کد حامل؛ برای سفارش قدیمی خالی می‌ماند.
/// </summary>
public sealed record ShippingAddress(
    string Version,
    string Province,
    string City,
    string? PostalCode,
    string FullAddress,
    string RecipientName,
    string RecipientPhone,
    string? CityDocumentId = null)
{
    public const int VersionMaxLength = 64;
    public const int ProvinceMaxLength = 100;
    public const int CityMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
    public const int FullAddressMaxLength = 1000;
    public const int RecipientNameMaxLength = 200;
    public const int RecipientPhoneMaxLength = 20;
    public const int CityDocumentIdMaxLength = 64;

    public bool IsValid() =>
        IsValidText(Version, VersionMaxLength) &&
        IsValidText(Province, ProvinceMaxLength) &&
        IsValidText(City, CityMaxLength) &&
        (PostalCode is null || IsValidText(PostalCode, PostalCodeMaxLength)) &&
        IsValidText(FullAddress, FullAddressMaxLength) &&
        IsValidText(RecipientName, RecipientNameMaxLength) &&
        IsValidText(RecipientPhone, RecipientPhoneMaxLength) &&
        (CityDocumentId is null || IsValidText(CityDocumentId, CityDocumentIdMaxLength));

    private static bool IsValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
}
