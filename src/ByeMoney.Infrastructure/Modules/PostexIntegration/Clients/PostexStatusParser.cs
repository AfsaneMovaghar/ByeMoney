using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Domain.Modules.Purchases;

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
    /// فقط ۱۱۳۱۱ استثناست: «شروع توزیع مجدد» زیر والد تحویل است و نباید تحویل ثبت شود.
    /// </summary>

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

            var trackingCode = ReadUsableTrackingCode(root);
            var trackingUrl = ReadHttpsTrackingUrl(root);
            var hasParent = TryReadNestedInt(root, "parent", "code", out var parent);
            int? parentCode = hasParent ? parent : null;

            // شروع توزیع مجدد زیر والد تحویل است؛ تحویل ثبت نمی‌شود.
            if (code == PhysicalProductPurchase.DeliveryRetryStatusCode)
            {
                return Result(TrackingOutcome.InTransit, statusName, statusTitle, trackingCode, code, parentCode, trackingUrl);
            }

            // خسارت و مفقودی حتی با والد دیگر هم تحویل یا برگشتی نمی‌شوند.
            if (PhysicalProductPurchase.IsDurableCarrierAlertCode(code))
            {
                return Result(
                    TrackingOutcome.InTransit, statusName, statusTitle, trackingCode, code, parentCode, trackingUrl,
                    durableAlert: true);
            }

            // مسیر ناموجود با والد ۸، یا بدون والد، برگشتی است. والدِ دیگر همان والد را تصمیم می‌گیرد.
            if (code == PhysicalProductPurchase.RouteNotFoundStatusCode &&
                (!hasParent || parent == PhysicalProductPurchase.CanceledParentCode))
            {
                return Result(TrackingOutcome.Returned, statusName, statusTitle, trackingCode, code, parentCode, trackingUrl);
            }

            var effectiveParent = hasParent ? parent : InferParentCode(code);
            var mismatch = code == PhysicalProductPurchase.RouteNotFoundStatusCode &&
                hasParent && parent != PhysicalProductPurchase.CanceledParentCode;
            var outcome = effectiveParent switch
            {
                DeliveredParentCode => TrackingOutcome.Delivered,
                ReturnedParentCode or CanceledParentCode => TrackingOutcome.Returned,
                _ => TrackingOutcome.InTransit
            };
            return Result(
                outcome, statusName, statusTitle, trackingCode, code, parentCode, trackingUrl, parentMismatch: mismatch);
        }
        catch (JsonException)
        {
            return new TrackingResult(TrackingOutcome.Failed);
        }
    }

    /// <summary>
    /// نشانی رهگیری از ریشه یا از <c>shipment[].tracking.tracking_url</c>. فقط https مطلق برمی‌گردد.
    /// </summary>
    public static string? ReadHttpsTrackingUrl(JsonElement root)
    {
        var raw = ReadString(root, "tracking_url");
        if (string.IsNullOrWhiteSpace(raw) &&
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("shipment", out var shipment) &&
            shipment.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in shipment.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("tracking", out var tracking) &&
                    tracking.ValueKind == JsonValueKind.Object)
                {
                    raw = ReadString(tracking, "tracking_url");
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        break;
                    }
                }
            }
        }

        return PhysicalProductPurchase.NormalizeHttpsTrackingUrl(raw);
    }

    private static TrackingResult Result(
        TrackingOutcome outcome,
        string? statusName,
        string? statusTitle,
        string? trackingCode,
        int statusCode,
        int? parentCode,
        string? trackingUrl,
        bool durableAlert = false,
        bool parentMismatch = false) =>
        new(outcome, statusName, statusTitle, trackingCode, statusCode, parentCode, trackingUrl, durableAlert, parentMismatch);

    /// <summary>
    /// اگر فقط کد فرزند آمده باشد، همان کدهای والد شناخته‌شده را برمی‌گرداند؛
    /// برای بقیهٔ فرزندان بدون والد، در مسیر باقی می‌ماند (نه تحویل قطعی).
    /// </summary>
    private static int InferParentCode(int statusCode) =>
        statusCode is DeliveredParentCode or ReturnedParentCode or CanceledParentCode
            ? statusCode
            : 0;

    private static string? ReadUsableTrackingCode(JsonElement root)
    {
        var tracking = ReadString(root, "tracking_number") ?? ReadString(root, "tracking_no");
        return PhysicalProductPurchase.IsUsablePostalTrackingCode(tracking) ? tracking!.Trim() : null;
    }

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
