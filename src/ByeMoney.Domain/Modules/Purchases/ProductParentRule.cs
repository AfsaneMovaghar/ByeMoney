namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// قاعدهٔ عام والد قلم: والد اختیاری است، ولی اگر باشد خالی نیست، حداکثر ۱۰۰ نویسه دارد و با شناسهٔ خود قلم برابر نیست.
/// قید وابسته به نوع (مثلاً «فصل باید والد داشته باشد») قاعدهٔ فروشنده است و در آداپتور همان فروشنده بررسی می‌شود.
/// </summary>
public static class ProductParentRule
{
    public const int MaxParentIdLength = 100;

    public static bool IsValid(string externalId, string? parentExternalId) =>
        parentExternalId is null ||
        (!string.IsNullOrWhiteSpace(parentExternalId) &&
         parentExternalId.Length <= MaxParentIdLength &&
         parentExternalId != externalId);
}
