using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;

/// <summary>
/// ساخت مرسوله در پستکس: اول خواندن با <c>custom-order-no</c>، بعد <c>parcels/bulk</c>.
/// نگاشت نام شهر گیرنده به <c>city_id</c> داخل همین کلاینت است تا واسط بعدی نگاشت خودش را داشته باشد.
/// </summary>
public sealed class PostexShipmentClient(HttpClient http, PostexOptions options) : IShipmentProviderClient
{
    private readonly SemaphoreSlim _cityCacheLock = new(1, 1);
    private Dictionary<string, int>? _cityIdsByName;

    public string Provider => PhysicalProductPurchase.PostexProvider;

    public bool IsConfigured => options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<ShipmentCreateResult> CreateOrGetParcelAsync(
        ShipmentCreateRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.NotConfigured,
                ErrorMessage: "واسط پستی پیکربندی نشده است.");
        }

        if (string.IsNullOrWhiteSpace(request.Parcel.IdempotencyKey))
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: "کلید یکتای مرسوله خالی است.");
        }

        var existing = await TryGetByCustomOrderNoAsync(request.Parcel.IdempotencyKey.Trim(), ct);
        if (existing is not null)
        {
            return existing with { Outcome = ShipmentCreateOutcome.AlreadyExists };
        }

        var cityId = await ResolveCityIdAsync(request.Recipient.CityName, request.Recipient.ProvinceName, ct);
        if (cityId is null)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: $"شهر گیرنده «{request.Recipient.CityName}» در فهرست پستکس یافت نشد.");
        }

        if (!IsSenderConfigured())
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: "تنظیمات فرستندهٔ پستکس ناقص است.");
        }

        try
        {
            using var response = await http.PostAsJsonAsync(
                "api/v1/parcels/bulk", BuildBulkBody(request, cityId.Value), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                var afterPayment = await TryGetByCustomOrderNoAsync(request.Parcel.IdempotencyKey.Trim(), ct);
                if (afterPayment is not null)
                {
                    return afterPayment with
                    {
                        Outcome = ShipmentCreateOutcome.PendingPayment,
                        ErrorMessage = "مرسوله ثبت شد ولی پرداخت نزد واسط معلق است."
                    };
                }

                return new ShipmentCreateResult(ShipmentCreateOutcome.PendingPayment,
                    ErrorMessage: ExtractErrorMessage(body) ?? "موجودی کیف پول واسط برای ثبت مرسوله کافی نیست.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                    ErrorMessage: ExtractErrorMessage(body) ?? $"ثبت مرسوله ناموفق بود ({(int)response.StatusCode}).");
            }

            var bulkFailed = TryReadBulkFailure(body);
            if (bulkFailed is not null)
            {
                // حتی وقتی HTTP 200 و isSuccess=false است، گاهی مرسوله ساخته شده؛ دوباره می‌خوانیم.
                var maybeCreated = await TryGetByCustomOrderNoAsync(request.Parcel.IdempotencyKey.Trim(), ct);
                if (maybeCreated is not null)
                {
                    return maybeCreated with { Outcome = ShipmentCreateOutcome.Created };
                }

                return new ShipmentCreateResult(ShipmentCreateOutcome.Failed, ErrorMessage: bulkFailed);
            }

            var created = await TryGetByCustomOrderNoAsync(request.Parcel.IdempotencyKey.Trim(), ct);
            if (created is not null)
            {
                return created with { Outcome = ShipmentCreateOutcome.Created };
            }

            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: "مرسوله ثبت شد ولی شناسهٔ ارجاع از واسط خوانده نشد.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return new ShipmentCreateResult(ShipmentCreateOutcome.Failed,
                ErrorMessage: "ارتباط با واسط پستی برقرار نشد.");
        }
    }

    private bool IsSenderConfigured() =>
        options.Sender.CityId > 0 &&
        !string.IsNullOrWhiteSpace(options.Sender.Address) &&
        !string.IsNullOrWhiteSpace(options.Sender.MobileNo) &&
        !string.IsNullOrWhiteSpace(options.Sender.TelephoneNo);

    private long ResolveDeclaredValueIrr(ShipmentParcelDetails parcel)
    {
        if (parcel.DeclaredValueMinorUnits > 0 &&
            string.Equals(parcel.CurrencyCode, "IRR", StringComparison.OrdinalIgnoreCase))
        {
            return parcel.DeclaredValueMinorUnits;
        }

        if (parcel.DeclaredValueMinorUnits > 0 &&
            string.Equals(parcel.CurrencyCode, "NOOR", StringComparison.OrdinalIgnoreCase))
        {
            var rate = options.RialPerNoorForDeclaredValue > 0 ? options.RialPerNoorForDeclaredValue : 10_000;
            return Math.Max(1, parcel.DeclaredValueMinorUnits * rate);
        }

        return Math.Max(1, options.DefaultParcelValueIrr);
    }

    private object BuildBulkBody(ShipmentCreateRequest request, int toCityId)
    {
        var (firstName, lastName) = SplitName(request.Recipient.FullName);
        var sender = options.Sender;
        var (senderFirst, senderLast) = (
            string.IsNullOrWhiteSpace(sender.FirstName) ? "فرستنده" : sender.FirstName.Trim(),
            string.IsNullOrWhiteSpace(sender.LastName) ? "شرکت" : sender.LastName.Trim());

        return new
        {
            collection_type = string.IsNullOrWhiteSpace(options.CollectionType) ? "pick_up" : options.CollectionType,
            remark = request.Remark,
            custom_channel = "api",
            parcels = new[]
            {
                new
                {
                    custom_order_no = request.Parcel.IdempotencyKey.Trim(),
                    ready_to_accept = options.ReadyToAccept,
                    submit_channel = "api",
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
                        location = new
                        {
                            address = sender.Address.Trim(),
                            city_id = sender.CityId,
                            city_name = sender.CityName,
                            country = "IR",
                            post_code = sender.PostCode
                        }
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
                            country = "IR",
                            post_code = request.Recipient.PostalCode
                        }
                    },
                    parcel_properties = new
                    {
                        box_type_id = options.DefaultBoxTypeId > 0 ? options.DefaultBoxTypeId : 5,
                        total_weight = request.Parcel.WeightGrams > 0
                            ? request.Parcel.WeightGrams
                            : Math.Max(1, options.DefaultWeightGrams),
                        total_value = ResolveDeclaredValueIrr(request.Parcel),
                        total_value_currency = "IRR",
                        pre_paid_amount = 0,
                        is_fragile = true,
                        is_liquid = false
                    },
                    courier = new
                    {
                        name = string.IsNullOrWhiteSpace(options.DefaultCourier) ? "IR_POST" : options.DefaultCourier,
                        service_type = string.IsNullOrWhiteSpace(options.DefaultServiceType)
                            ? "EXPRESS"
                            : options.DefaultServiceType,
                        payment_type = "SENDER"
                    }
                }
            }
        };
    }

    private async Task<ShipmentCreateResult?> TryGetByCustomOrderNoAsync(string customOrderNo, CancellationToken ct)
    {
        var path = $"api/v1/parcels/custom-order-no/{Uri.EscapeDataString(customOrderNo)}";
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return ParseParcelDetails(body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return null;
        }
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

        return new ShipmentCreateResult(
            ShipmentCreateOutcome.Created,
            ProviderParcelId: parcelNo,
            TrackingCode: IsUsableTracking(tracking) ? tracking : null,
            StatusName: statusName);
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

    private static bool IsUsableTracking(string? tracking) =>
        !string.IsNullOrWhiteSpace(tracking) &&
        tracking.Any(char.IsLetterOrDigit) &&
        !tracking.All(ch => ch == '*');

    private async Task<int?> ResolveCityIdAsync(string cityName, string provinceName, CancellationToken ct)
    {
        var map = await GetCityMapAsync(ct);
        var cityKey = NormalizeName(cityName);
        if (map.TryGetValue(cityKey, out var id))
        {
            return id;
        }

        // بعضی آدرس‌ها «مشهد مقدس» دارند؛ پیشوند نام شهر را هم امتحان می‌کنیم.
        var provinceKey = NormalizeName(provinceName);
        foreach (var pair in map)
        {
            if (pair.Key.StartsWith(cityKey, StringComparison.Ordinal) ||
                cityKey.StartsWith(pair.Key, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        _ = provinceKey;
        return null;
    }

    private async Task<Dictionary<string, int>> GetCityMapAsync(CancellationToken ct)
    {
        if (_cityIdsByName is not null)
        {
            return _cityIdsByName;
        }

        await _cityCacheLock.WaitAsync(ct);
        try
        {
            if (_cityIdsByName is not null)
            {
                return _cityIdsByName;
            }

            using var response = await http.GetAsync("api/v1/locality/cities/all", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var province in document.RootElement.EnumerateArray())
            {
                if (!province.TryGetProperty("cities", out var cities) ||
                    cities.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var city in cities.EnumerateArray())
                {
                    var name = ReadString(city, "name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var id = city.TryGetProperty("id", out var idProperty) && idProperty.TryGetInt32(out var cityId)
                        ? cityId
                        : 0;
                    if (id > 0)
                    {
                        map[NormalizeName(name)] = id;
                    }
                }
            }

            _cityIdsByName = map;
            return map;
        }
        finally
        {
            _cityCacheLock.Release();
        }
    }

    private static string NormalizeName(string value) =>
        value.Trim().Replace("ي", "ی").Replace("ك", "ک").Replace(" ", string.Empty);

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return ("گیرنده", "نامشخص");
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
                    return ReadString(item, "message") ?? "ثبت مرسوله توسط واسط رد شد.";
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ExtractErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadString(document.RootElement, "Message")
                ?? ReadString(document.RootElement, "message")
                ?? ReadString(document.RootElement, "title");
        }
        catch (JsonException)
        {
            return null;
        }
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
