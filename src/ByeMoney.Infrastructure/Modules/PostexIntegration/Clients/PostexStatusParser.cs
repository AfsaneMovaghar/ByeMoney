using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;

/// <summary>
/// تفسیر پاسخ واقعی استعلام وضعیت پستکس (<c>/parcels/{no}/status</c>).
/// معیار قطعی کد عددی والد (<c>parent.code</c>) و در نبود آن <c>status_code</c> است؛
/// نام متنی یکتا نیست و برای تصمیم تحویل استفاده نمی‌شود (بند ۷.۳ سند Postex_API).
/// </summary>
public static class PostexStatusParser
{
    /// <summary>والد «تحویل شده» در فهرست وضعیت‌های پستکس.</summary>
    private const int DeliveredParentCode = 4;

    /// <summary>والد «برگشتی».</summary>
    private const int ReturnedParentCode = 5;

    /// <summary>والد «لغو شده»؛ مثل برگشتی رسیدگی دستی می‌خواهد.</summary>
    private const int CanceledParentCode = 8;

    /// <summary>
    /// کدهایی که والدشان با معنای واقعی هم‌خوان نیست (بند ۷.۳):
    /// توزیع مجدد زیر والد تحویل، و مسیر ناموجود زیر والد لغو.
    /// </summary>
    private static readonly HashSet<int> ExceptionStatusCodes = [11311, 11310];

    public static TrackingResult Parse(string? json, int statusCode)
    {
        if (statusCode == 404)
        {
            return new TrackingResult(TrackingOutcome.NotFound);
        }

        if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(json))
        {
            // پستکس برای مرسولهٔ ناموجود گاهی ۴۰۰ با پیام Parcel ... not found می‌دهد.
            if (statusCode == 400 && LooksLikeNotFound(json))
            {
                return new TrackingResult(TrackingOutcome.NotFound);
            }

            return new TrackingResult(TrackingOutcome.Failed);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var statusName = ReadString(root, "status_name") ?? ReadNestedString(root, "parent", "name");
            var statusTitle = ReadString(root, "status_title") ?? ReadNestedString(root, "parent", "description");

            if (!TryReadInt(root, "status_code", out var code) &&
                !TryReadNestedInt(root, "parent", "code", out code))
            {
                return new TrackingResult(TrackingOutcome.Failed, statusName, statusTitle);
            }

            if (ExceptionStatusCodes.Contains(code))
            {
                return new TrackingResult(TrackingOutcome.InTransit, statusName, statusTitle);
            }

            var parentCode = TryReadNestedInt(root, "parent", "code", out var parent)
                ? parent
                : InferParentCode(code);

            return parentCode switch
            {
                DeliveredParentCode => new TrackingResult(TrackingOutcome.Delivered, statusName, statusTitle),
                ReturnedParentCode or CanceledParentCode =>
                    new TrackingResult(TrackingOutcome.Returned, statusName, statusTitle),
                _ => new TrackingResult(TrackingOutcome.InTransit, statusName, statusTitle)
            };
        }
        catch (JsonException)
        {
            return new TrackingResult(TrackingOutcome.Failed);
        }
    }

    /// <summary>
    /// اگر فقط کد فرزند آمده باشد، همان کدهای والد شناخته‌شده را برمی‌گرداند؛
    /// برای بقیهٔ فرزندان بدون والد، در مسیر باقی می‌ماند (نه تحویل قطعی).
    /// </summary>
    private static int InferParentCode(int statusCode) =>
        statusCode is DeliveredParentCode or ReturnedParentCode or CanceledParentCode
            ? statusCode
            : 0;

    private static bool LooksLikeNotFound(string? json) =>
        !string.IsNullOrWhiteSpace(json) &&
        json.Contains("not found", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(objectName, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(nested, propertyName);
    }

    private static bool TryReadInt(JsonElement element, string name, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadNestedInt(JsonElement element, string objectName, string propertyName, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(objectName, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return TryReadInt(nested, propertyName, out value);
    }
}
