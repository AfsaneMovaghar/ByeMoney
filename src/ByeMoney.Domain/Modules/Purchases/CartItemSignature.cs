namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// قالب یکتای اثر انگشت قلم سبد. سبد پیش از دریافت قیمت ردیفی ندارد، پس تنها سند درخواست
/// خریدار همین اثر انگشت است؛ قالب و تجزیه فقط در همین نوع نگهداری می‌شوند.
/// </summary>
public static class CartItemSignature
{
    private const char Separator = ':';

    public static string Format(string externalId, ProductItemType type, int quantity) =>
        type is ProductItemType.Course or ProductItemType.CourseChapter && quantity == 1
            ? externalId
            : $"{type}{Separator}{externalId}{Separator}{quantity}";

    /// <summary>
    /// اثر انگشت تایپ‌دار را به شناسه، نوع و تعداد برمی‌گرداند. رشته‌ای که قالب تایپ‌دار ندارد
    /// یک شناسهٔ دوره با تعداد یک است؛ بنابراین اثر انگشت‌های قدیمی و ساده نیز بدون تغییر خوانده می‌شوند.
    /// </summary>
    public static CartItemInput Parse(string signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var firstSeparator = signature.IndexOf(Separator);
        var lastSeparator = signature.LastIndexOf(Separator);
        if (firstSeparator <= 0 || lastSeparator == firstSeparator || lastSeparator == signature.Length - 1)
        {
            return new CartItemInput(signature, ProductItemType.Course, 1);
        }

        var type = ParseType(signature[..firstSeparator]);
        if (type is null ||
            !int.TryParse(signature[(lastSeparator + 1)..], out var quantity) ||
            quantity < 1)
        {
            return new CartItemInput(signature, ProductItemType.Course, 1);
        }

        var externalId = signature[(firstSeparator + 1)..lastSeparator];
        return string.IsNullOrEmpty(externalId)
            ? new CartItemInput(signature, ProductItemType.Course, 1)
            : new CartItemInput(externalId, type.Value, quantity);
    }

    private static ProductItemType? ParseType(string prefix) => prefix switch
    {
        nameof(ProductItemType.Course) => ProductItemType.Course,
        nameof(ProductItemType.CourseChapter) => ProductItemType.CourseChapter,
        nameof(ProductItemType.Product) => ProductItemType.Product,
        _ => null
    };
}
