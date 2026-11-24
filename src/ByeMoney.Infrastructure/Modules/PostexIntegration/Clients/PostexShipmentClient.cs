using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;

/// <summary>
/// ساخت مرسوله در پستکس: اول خواندن با <c>custom-order-no</c>، بعد <c>parcels/bulk</c>.
/// کد شهر گیرنده از جدول محل خوانده می‌شود و از فهرست شهر پستکس پیدا نمی‌شود.
/// </summary>
public sealed class PostexShipmentClient(
    HttpClient http,
    PostexOptions options,
    IDeliveryCityCodeReader cityCodes) : IShipmentProviderClient
{
    public string Provider => PhysicalProductPurchase.PostexProvider;

    public bool IsConfigured => options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<ShipmentCreateResult> CreateOrGetParcelAsync(
        ShipmentCreateRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.NotConfigured,
                ErrorMessage: OrderErrors.ShipmentNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.Parcel.IdempotencyKey))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentIdempotencyKeyRequired);
        }

        if (!IsParcelComplete(request.Parcel))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentParcelRequired);
        }

        if (!TryResolveCollection(request.CollectionType, out var collectionType, out var courierPickup))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentCollectionTypeRequired);
        }

        var sender = options.ResolveOrigin(request.OriginId);
        if (sender is null)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentOriginNotConfigured);
        }

        if (IsSampleProfile(sender))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentOriginSampleProfile);
        }

        if (!IsSenderConfigured(sender))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentOriginProfileIncomplete);
        }

        if (courierPickup && !HasCoordinates(sender))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentOriginCoordinatesRequired);
        }

        var existing = await ReadParcelAsync(request.Parcel.IdempotencyKey.Trim(), ct);
        if (existing.Kind == ParcelReadKind.Failed)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentReadFailed);
        }

        if (existing.Kind == ParcelReadKind.Found)
        {
            return AsExisting(existing.Parcel!);
        }

        var cityCode = await cityCodes.ReadAsync(
            request.Recipient.CityDocumentId,
            request.Recipient.ProvinceName,
            request.Recipient.CityName,
            ct);
        if (cityCode.Status != DeliveryCityCodeStatus.Found || cityCode.Code is not int cityId || cityId <= 0)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: cityCode.Status == DeliveryCityCodeStatus.Unavailable
                    ? OrderErrors.ShipmentCityUnavailable
                    : OrderErrors.ShipmentCityNotFound);
        }

        try
        {
            using var response = await http.PostAsJsonAsync(
                "api/v1/parcels/bulk",
                BuildBulkBody(request, cityId, sender, collectionType, courierPickup),
                ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                var afterPayment = await ReadParcelAsync(request.Parcel.IdempotencyKey.Trim(), ct);
                if (afterPayment.Kind == ParcelReadKind.Failed)
                {
                    // مرسوله احتمالاً ساخته شده و شناسه‌اش خوانده نشد؛ رزرو آزاد نمی‌شود.
                    return new ShipmentCreateResult(ShipmentCreateOutcome.Uncertain,
                        ErrorMessage: OrderErrors.ShipmentReadFailed);
                }

                if (afterPayment.Kind == ParcelReadKind.Found)
                {
                    return afterPayment.Parcel! with
                    {
                        Outcome = ShipmentCreateOutcome.PendingPayment,
                        ErrorMessage = OrderErrors.ShipmentPaymentPending
                    };
                }

                return new ShipmentCreateResult(ShipmentCreateOutcome.PendingPayment,
                    ErrorMessage: ExtractErrorMessage(body) ?? OrderErrors.ShipmentPaymentPending);
            }

            if (!response.IsSuccessStatusCode)
            {
                // خطای ۵xx یعنی پاسخ قطعی نیست و شاید مرسوله ثبت شده باشد؛ بقیهٔ 4xx رد صریح است.
                var outcome = (int)response.StatusCode >= 500
                    ? ShipmentCreateOutcome.Uncertain
                    : ShipmentCreateOutcome.Failed;

                return new ShipmentCreateResult(outcome,
                    ErrorMessage: PresentError(
                        collectionType,
                        ExtractErrorMessage(body) ?? string.Format(
                            OrderErrors.ShipmentCreateHttpFailed, (int)response.StatusCode),
                        body));
            }

            var bulkFailed = TryReadBulkFailure(body);
            if (bulkFailed is not null)
            {
                // حتی وقتی HTTP 200 و isSuccess=false است، گاهی مرسوله ساخته شده؛ دوباره می‌خوانیم.
                return await ResultAfterWriteAsync(
                    request.Parcel.IdempotencyKey.Trim(),
                    PresentError(collectionType, bulkFailed, body)!,
                    ct);
            }

            var created = await ReadParcelAsync(request.Parcel.IdempotencyKey.Trim(), ct);
            if (created.Kind == ParcelReadKind.Failed)
            {
                return new ShipmentCreateResult(ShipmentCreateOutcome.Uncertain,
                    ErrorMessage: OrderErrors.ShipmentReadFailed);
            }

            if (created.Kind == ParcelReadKind.Found)
            {
                return AsWritten(created.Parcel!);
            }

            // درخواست موفق بوده ولی مرسوله خوانده نشد؛ ممکن است بعداً ظاهر شود.
            return new ShipmentCreateResult(ShipmentCreateOutcome.Uncertain,
                ErrorMessage: OrderErrors.ShipmentReferenceMissing);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // درخواست ممکن است به واسط رسیده باشد؛ نتیجه نامعلوم است و رزرو باید بماند.
            return new ShipmentCreateResult(ShipmentCreateOutcome.Uncertain,
                ErrorMessage: OrderErrors.ShipmentOutcomeUnknown);
        }
    }

    /// <summary>
    /// خواندن مرسوله با کلید یکتا؛ هیچ ساخت یا تغییری انجام نمی‌شود.
    /// </summary>
    public async Task<ShipmentCreateResult> GetParcelAsync(string idempotencyKey, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.NotConfigured,
                ErrorMessage: OrderErrors.ShipmentNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentIdempotencyKeyRequired);
        }

        var read = await ReadParcelAsync(idempotencyKey.Trim(), ct);
        if (read.Kind == ParcelReadKind.Found)
        {
            return read.Parcel!;
        }

        return read.Kind == ParcelReadKind.NotFound
            ? new ShipmentCreateResult(ShipmentCreateOutcome.NotFound)
            : new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: OrderErrors.ShipmentReadFailed);
    }

    private const string SampleMobile = "09120000000";
    private const string SampleTelephone = "02100000000";
    private const string SampleAddress = "رامسر";

    /// <summary>
    /// پروفایل کلاس پیش‌فرض: موبایل یا تلفن نمونه، یا نشانی‌ای که فقط «رامسر» است.
    /// نام شرکت «خاک تا افلاک» به‌تنهایی نشانهٔ نمونه نیست.
    /// </summary>
    private static bool IsSampleProfile(PostexSenderOptions sender) =>
        string.Equals(sender.MobileNo?.Trim(), SampleMobile, StringComparison.Ordinal) ||
        string.Equals(sender.TelephoneNo?.Trim(), SampleTelephone, StringComparison.Ordinal) ||
        string.Equals(sender.Address?.Trim(), SampleAddress, StringComparison.Ordinal);

    private static bool IsSenderConfigured(PostexSenderOptions sender) =>
        sender.CityId > 0 &&
        !string.IsNullOrWhiteSpace(sender.Address) &&
        !string.IsNullOrWhiteSpace(sender.MobileNo) &&
        !string.IsNullOrWhiteSpace(sender.TelephoneNo);

    private static bool IsParcelComplete(ShipmentParcelDetails parcel)
    {
        if (parcel.Items is null ||
            parcel.Items.Count == 0 ||
            parcel.TotalWeightGrams < 1 ||
            parcel.TotalValueIrr < 1 ||
            parcel.LengthCm < 1 ||
            parcel.WidthCm < 1 ||
            parcel.HeightCm < 1 ||
            parcel.BoxTypeId < ParcelFacts.MinBoxTypeId ||
            parcel.BoxTypeId > ParcelFacts.MaxBoxTypeId)
        {
            return false;
        }

        foreach (var item in parcel.Items)
        {
            if (item.Quantity < 1 || item.WeightGrams < 1 || item.ValueIrr < 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveCollection(string? collectionType, out string normalized, out bool courierPickup)
    {
        if (!ShippingCollectionTypes.IsAllowed(collectionType, out normalized))
        {
            courierPickup = false;
            return false;
        }

        courierPickup = normalized == ShippingCollectionTypes.PickUp;
        return true;
    }

    private static string? PresentError(string collectionType, string? message, string? rawBody)
    {
        if (collectionType is ShippingCollectionTypes.CourierDropOff or ShippingCollectionTypes.PostexDropOff)
        {
            var haystack = (message ?? string.Empty) + "\n" + (rawBody ?? string.Empty);
            if (haystack.Contains(PostexShipmentContract.DropOffLocationMarker, StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains(PostexShipmentContract.DropOffLocationMarkerPascal, StringComparison.Ordinal))
            {
                return OrderErrors.ShipmentDropOffOfficeRequired;
            }
        }

        return message;
    }

    private static bool HasCoordinates(PostexSenderOptions sender) =>
        sender.Lat is >= -90 and <= 90 &&
        sender.Lon is >= -180 and <= 180;

    /// <summary>عرض و طول فقط برای پیک فرستاده می‌شوند.</summary>
    private static Dictionary<string, object?> SenderLocation(PostexSenderOptions sender, bool courierPickup)
    {
        var location = new Dictionary<string, object?>
        {
            ["address"] = sender.Address.Trim(),
            ["city_id"] = sender.CityId,
            ["city_name"] = sender.CityName,
            ["country"] = PostexShipmentContract.CountryCode,
            ["post_code"] = sender.PostCode,
        };
        if (courierPickup)
        {
            location["lat"] = sender.Lat;
            location["lon"] = sender.Lon;
        }

        return location;
    }

    private object BuildBulkBody(
        ShipmentCreateRequest request,
        int toCityId,
        PostexSenderOptions sender,
        string collectionType,
        bool courierPickup)
    {
        var (firstName, lastName) = SplitName(request.Recipient.FullName);
        var (senderFirst, senderLast) = (
            string.IsNullOrWhiteSpace(sender.FirstName)
                ? PostexShipmentContract.FallbackSenderFirstName
                : sender.FirstName.Trim(),
            string.IsNullOrWhiteSpace(sender.LastName)
                ? PostexShipmentContract.FallbackSenderLastName
                : sender.LastName.Trim());
        var parcel = request.Parcel;

        return new
        {
            collection_type = collectionType,
            remark = request.Remark,
            custom_channel = PostexShipmentContract.ApiChannel,
            parcels = new[]
            {
                new
                {
                    custom_order_no = request.Parcel.IdempotencyKey.Trim(),
                    ready_to_accept = courierPickup,
                    submit_channel = PostexShipmentContract.ApiChannel,
                    from = new
                    {
                        contact = new
                        {
                            first_name = senderFirst,
                            last_name = senderLast,
                            mobile_no = sender.MobileNo.Trim(),
                            telephone_no = sender.TelephoneNo.Trim(),
                            company_name = sender.CompanyName,
                            national_code = sender.NationalCode
                        },
                        location = SenderLocation(sender, courierPickup)
                    },
                    to = new
                    {
                        contact = new
                        {
                            first_name = firstName,
                            last_name = lastName,
                            mobile_no = request.Recipient.Mobile.Trim(),
                            telephone_no = string.IsNullOrWhiteSpace(request.Recipient.Telephone)
                                ? request.Recipient.Mobile.Trim()
                                : request.Recipient.Telephone.Trim(),
                            national_code = request.Recipient.NationalCode
                        },
                        location = new
                        {
                            address = request.Recipient.Address.Trim(),
                            city_id = toCityId,
                            city_name = request.Recipient.CityName.Trim(),
                            country = PostexShipmentContract.CountryCode,
                            post_code = request.Recipient.PostalCode
                        }
                    },
                    parcel_items = parcel.Items.Select(item => new
                    {
                        description = item.Description,
                        quantity = item.Quantity,
                        weight = item.WeightGrams,
                        price = item.ValueIrr
                    }).ToArray(),
                    parcel_properties = new
                    {
                        box_type_id = parcel.BoxTypeId,
                        length = parcel.LengthCm,
                        width = parcel.WidthCm,
                        height = parcel.HeightCm,
                        total_weight = parcel.TotalWeightGrams,
                        total_value = parcel.TotalValueIrr,
                        total_value_currency = PostexShipmentContract.ValueCurrency,
                        pre_paid_amount = PostexShipmentContract.PrepaidAmount,
                        is_fragile = parcel.IsFragile,
                        is_liquid = PostexShipmentContract.ShipsLiquid
                    },
                    courier = new
                    {
                        name = ShippingDispatchCodes.NormalizeCourier(request.CourierCode),
                        service_type = ShippingDispatchCodes.NormalizeService(request.ServiceType),
                        payment_type = PostexShipmentContract.FreightPayer
                    },
                    added_service = new
                    {
                        request_label = PostexShipmentContract.RequestLabel,
                        request_packaging = PostexShipmentContract.RequestPackaging,
                        request_sms_notification = PostexShipmentContract.RequestSmsNotification
                    }
                }
            }
        };
    }

    private async Task<ShipmentCreateResult> ResultAfterWriteAsync(
        string customOrderNo, string fallbackError, CancellationToken ct)
    {
        var read = await ReadParcelAsync(customOrderNo, ct);
        if (read.Kind == ParcelReadKind.Failed)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Uncertain,
                ErrorMessage: OrderErrors.ShipmentReadFailed);
        }

        if (read.Kind == ParcelReadKind.Found)
        {
            return AsWritten(read.Parcel!);
        }

        return new ShipmentCreateResult(ShipmentCreateOutcome.Failed, ErrorMessage: fallbackError);
    }

    private static ShipmentCreateResult AsExisting(ShipmentCreateResult parcel) =>
        parcel.Outcome == ShipmentCreateOutcome.PendingPayment
            ? parcel with { ErrorMessage = OrderErrors.ShipmentPaymentPending }
            : parcel with { Outcome = ShipmentCreateOutcome.AlreadyExists };

    private static ShipmentCreateResult AsWritten(ShipmentCreateResult parcel) =>
        parcel.Outcome == ShipmentCreateOutcome.PendingPayment
            ? parcel with { ErrorMessage = OrderErrors.ShipmentPaymentPending }
            : parcel with { Outcome = ShipmentCreateOutcome.Created };

    private async Task<ParcelRead> ReadParcelAsync(string customOrderNo, CancellationToken ct)
    {
        var path = $"api/v1/parcels/custom-order-no/{Uri.EscapeDataString(customOrderNo)}";
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ParcelRead.NotFound();
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return ParcelRead.Failed();
            }

            var parsed = ParseParcelDetails(body);
            return parsed is null ? ParcelRead.Failed() : ParcelRead.Found(parsed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return ParcelRead.Failed();
        }
    }

    private enum ParcelReadKind { NotFound, Found, Failed }

    private readonly record struct ParcelRead(ParcelReadKind Kind, ShipmentCreateResult? Parcel)
    {
        public static ParcelRead NotFound() => new(ParcelReadKind.NotFound, null);

        public static ParcelRead Found(ShipmentCreateResult parcel) => new(ParcelReadKind.Found, parcel);

        public static ParcelRead Failed() => new(ParcelReadKind.Failed, null);
    }

    private static ShipmentCreateResult? ParseParcelDetails(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!TryReadParcelNo(root, out var parcelNo))
        {
            return null;
        }

        var tracking = ReadTrackingNumber(root);
        var statusName = ReadString(root, "status_name")
            ?? ReadNestedString(root, "current_status", "name")
            ?? ReadNestedString(root, "current_status", "status_name");

        var unpaid = IsPaymentPending(root);
        return new ShipmentCreateResult(
            unpaid ? ShipmentCreateOutcome.PendingPayment : ShipmentCreateOutcome.Created,
            ProviderParcelId: parcelNo,
            TrackingCode: PhysicalProductPurchase.IsUsablePostalTrackingCode(tracking) ? tracking!.Trim() : null,
            StatusName: statusName,
            ErrorMessage: unpaid ? OrderErrors.ShipmentPaymentPending : null,
            TrackingUrl: PostexStatusParser.ReadHttpsTrackingUrl(root));
    }

    private static bool TryReadParcelNo(JsonElement root, out string parcelNo)
    {
        parcelNo = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("parcel_no", out var property))
        {
            if (property.ValueKind == JsonValueKind.Number)
            {
                parcelNo = property.GetInt64().ToString();
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                parcelNo = property.GetString()!.Trim();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// کدهای «در انتظار پرداخت» در <see cref="PostexShipmentContract"/> هستند.
    /// نام فقط وقتی کد عددی در پاسخ نیست به کار می‌رود.
    /// </summary>
    private static bool IsPaymentPending(JsonElement root)
    {
        if (ReadNestedInt(root, "parent", "code") == PostexShipmentContract.PaymentPendingCode ||
            ReadInt(root, "status_code") is PostexShipmentContract.PaymentPendingCode
                or PostexShipmentContract.PaymentPendingStatusCode)
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("current_status", out var current) &&
            current.ValueKind == JsonValueKind.Object &&
            (ReadNestedInt(current, "parent", "code") == PostexShipmentContract.PaymentPendingCode ||
                ReadInt(current, "code") == PostexShipmentContract.PaymentPendingCode ||
                ReadInt(current, "status_code") is PostexShipmentContract.PaymentPendingCode
                    or PostexShipmentContract.PaymentPendingStatusCode))
        {
            return true;
        }

        var name = ReadString(root, "status_name")
            ?? ReadNestedString(root, "current_status", "name")
            ?? ReadNestedString(root, "current_status", "status_name")
            ?? ReadNestedString(root, "parent", "name");
        return string.Equals(name, PostexShipmentContract.PaymentPendingStatusName, StringComparison.OrdinalIgnoreCase);
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static int? ReadNestedInt(JsonElement element, string objectName, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(objectName, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadInt(nested, propertyName);
    }

    private static string? ReadTrackingNumber(JsonElement root)
    {
        var direct = ReadString(root, "tracking_number") ?? ReadString(root, "tracking_no");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (root.TryGetProperty("shipment", out var shipment) && shipment.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in shipment.EnumerateArray())
            {
                if (item.TryGetProperty("tracking", out var tracking) &&
                    tracking.ValueKind == JsonValueKind.Object)
                {
                    var code = ReadString(tracking, "tracking_number")
                        ?? ReadString(tracking, "barcode")
                        ?? ReadString(tracking, "tracking_no");
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        return code;
                    }
                }
            }
        }

        return null;
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (
                PostexShipmentContract.FallbackRecipientFirstName,
                PostexShipmentContract.FallbackRecipientLastName);
        }

        if (parts.Length == 1)
        {
            return (parts[0], parts[0]);
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string? TryReadBulkFailure(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in result.EnumerateArray())
            {
                var success = item.TryGetProperty("isSuccess", out var flag) &&
                    flag.ValueKind == JsonValueKind.True;
                if (!success)
                {
                    return ReadString(item, "message") ?? OrderErrors.ShipmentRejectedByCarrier;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// پیام خطای پستکس چند قالب دارد؛ علاوه بر پیام کلی، خطاهای فیلدی
    /// <c>errors</c> و <c>invalid_fields</c> هم با نام فیلد برگردانده می‌شوند.
    /// </summary>
    private static string? ExtractErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var messages = new List<string>();
            AddMessage(messages, ReadString(root, "Message"));
            AddMessage(messages, ReadString(root, "message"));
            AddMessage(messages, ReadString(root, "title"));
            AddFieldMessages(messages, root, "errors");
            AddFieldMessages(messages, root, "invalid_fields");
            return messages.Count == 0 ? null : string.Join(" | ", messages);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddMessage(List<string> messages, string? message)
    {
        var trimmed = message?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && !messages.Contains(trimmed!))
        {
            messages.Add(trimmed!);
        }
    }

    private static void AddFieldMessages(List<string> messages, JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var field in fields.EnumerateObject())
        {
            var text = ReadFieldMessage(field.Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                messages.Add($"{field.Name}: {text}");
            }
        }
    }

    private static string? ReadFieldMessage(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var messages = value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return messages.Length == 0 ? null : string.Join("; ", messages);
    }

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
}
