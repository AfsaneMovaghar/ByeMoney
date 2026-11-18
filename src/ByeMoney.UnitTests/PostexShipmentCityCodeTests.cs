using System.Net;
using System.Text;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Modules.PostexIntegration;
using ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class PostexShipmentCityCodeTests
{
    [Fact]
    public async Task CreatesParcelWithCarrierCodeAndDoesNotListPostexCities()
    {
        var http = new RecordingHandler();
        var reader = new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) };
        var client = Client(http, reader);

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        reader.CityDocumentId.Should().Be("city-mashhad");
        reader.Province.Should().Be("خراسان رضوی");
        reader.City.Should().Be("مشهد");
        http.Paths.Should().NotContain(path => path.Contains("locality/cities", StringComparison.Ordinal));
        http.Paths.Should().Contain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
        http.LastPostBody.Should().Contain("\"city_id\":301");
    }

    [Fact]
    public async Task LegacyOrderLooksUpExactStoredNames()
    {
        var http = new RecordingHandler();
        var reader = new FakeCityCodeReader { Result = DeliveryCityCodeRead.NotFound() };
        var client = Client(http, reader);

        var result = await client.CreateOrGetParcelAsync(Request(cityDocumentId: null), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentCityNotFound);
        reader.CityDocumentId.Should().BeNull();
        reader.Province.Should().Be("خراسان رضوی");
        reader.City.Should().Be("مشهد");
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
        http.Paths.Should().NotContain(path => path.Contains("locality/cities", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingParcelSkipsCityLookup()
    {
        var http = new RecordingHandler { ExistingParcel = true };
        var reader = new FakeCityCodeReader();
        var client = Client(http, reader);

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.AlreadyExists);
        result.ProviderParcelId.Should().Be("99");
        reader.Calls.Should().Be(0);
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadFailureDoesNotCreateAnotherParcel()
    {
        var http = new RecordingHandler { ReadStatus = HttpStatusCode.InternalServerError };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentReadFailed);
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SuccessWithoutParcelNumberDoesNotCreateAnotherParcel()
    {
        var http = new RecordingHandler { ExistingParcel = true, ExistingBody = """{"status_name":"IN_TRANSIT"}""" };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentReadFailed);
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnpaidExistingParcelIsPendingAndSkipsCreate()
    {
        var http = new RecordingHandler
        {
            ExistingParcel = true,
            ExistingBody = """{"parcel_no":99,"parent":{"code":1,"name":"PAYMENT_PENDING"},"status_code":10000,"tracking_number":"**********"}"""
        };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.PendingPayment);
        result.ProviderParcelId.Should().Be("99");
        result.TrackingCode.Should().BeNull();
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentPaymentPending);
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PaymentRequiredResponseStaysPendingAfterReread()
    {
        var http = new RecordingHandler { PaymentRequired = true };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.PendingPayment);
        result.ProviderParcelId.Should().Be("1319906676294");
        http.Paths.Count(path => path.Contains("parcels/bulk", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task IncompleteParcelIsRejectedBeforeReadOrBulk()
    {
        var http = new RecordingHandler();
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });
        var request = Request("city-mashhad") with
        {
            Parcel = new ShipmentParcelDetails(
                "BM-1", "شمش", 1, new ParcelFacts(0, 15, 10, 5, 60_000_000, true))
        };

        var result = await client.CreateOrGetParcelAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentParcelRequired);
        http.Paths.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkBodyCarriesFrozenParcelFactsAndNoBoxType()
    {
        var http = new RecordingHandler();
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        using var body = JsonDocument.Parse(http.LastPostBody!);
        var parcel = body.RootElement.GetProperty("parcels").EnumerateArray().Single();
        var properties = parcel.GetProperty("parcel_properties");
        properties.GetProperty("length").GetInt32().Should().Be(15);
        properties.GetProperty("width").GetInt32().Should().Be(10);
        properties.GetProperty("height").GetInt32().Should().Be(5);
        properties.GetProperty("total_weight").GetInt32().Should().Be(750);
        properties.GetProperty("total_value").GetInt64().Should().Be(60_000_000);
        properties.GetProperty("is_fragile").GetBoolean().Should().BeTrue();
        properties.TryGetProperty("box_type_id", out _).Should().BeFalse();

        var item = parcel.GetProperty("parcel_items").EnumerateArray().Single();
        item.GetProperty("description").GetString().Should().Be("شمش");
        item.GetProperty("quantity").GetInt32().Should().Be(1);
        item.GetProperty("weight").GetInt32().Should().Be(750);
        item.GetProperty("price").GetInt64().Should().Be(60_000_000);

        parcel.GetProperty("courier").GetProperty("name").GetString().Should().Be("IR_POST");
        parcel.GetProperty("courier").GetProperty("service_type").GetString().Should().Be("EXPRESS");
        parcel.GetProperty("courier").GetProperty("payment_type").GetString().Should().Be("SENDER");
        parcel.TryGetProperty("added_service", out _).Should().BeFalse();
        parcel.GetProperty("from").GetProperty("location").GetProperty("lat").GetDouble().Should().Be(36.92);
        parcel.GetProperty("from").GetProperty("location").GetProperty("lon").GetDouble().Should().Be(50.65);
        body.RootElement.GetProperty("collection_type").GetString().Should().Be("pick_up");
        parcel.GetProperty("ready_to_accept").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PickUpWithoutOriginCoordinatesStopsBeforeBulk()
    {
        var http = new RecordingHandler();
        var reader = new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) };
        var client = Client(
            http,
            reader,
            lat: null,
            lon: null);

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentOriginCoordinatesRequired);
        reader.Calls.Should().Be(0);
        http.Paths.Should().NotContain(path => path.Contains("parcels/bulk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DropOffDoesNotNeedCoordinatesAndKeepsCollectionType()
    {
        var http = new RecordingHandler();
        var client = Client(
            http,
            new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) },
            lat: null,
            lon: null);

        await client.CreateOrGetParcelAsync(
            Request("city-mashhad", "courier_drop_off"), CancellationToken.None);

        using var body = JsonDocument.Parse(http.LastPostBody!);
        body.RootElement.GetProperty("collection_type").GetString().Should().Be("courier_drop_off");
        var parcel = body.RootElement.GetProperty("parcels").EnumerateArray().Single();
        parcel.GetProperty("ready_to_accept").GetBoolean().Should().BeFalse();
        parcel.GetProperty("from").GetProperty("location").GetProperty("lat").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DropOffRejectedForMissingOfficeReturnsDomainMessage()
    {
        const string body = """{"invalid_fields":{"drop_off_location_id":["required"]},"message":"خطا"}""";
        var http = new RecordingHandler { BulkStatus = HttpStatusCode.BadRequest, BulkBody = body };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) }, lat: null, lon: null);

        var result = await client.CreateOrGetParcelAsync(
            Request("city-mashhad", "courier_drop_off"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Be(OrderErrors.ShipmentDropOffOfficeRequired);
    }

    [Fact]
    public async Task ValidationErrorsAndInvalidFieldsAreIncludedInMessage()
    {
        const string modelErrors = """{"title":"One or more validation errors occurred.","status":400,"errors":{"Parcels[0].To.Contact.TelephoneNo":["The TelephoneNo field is required."]},"traceId":"0af"}""";
        var http = new RecordingHandler { BulkBody = modelErrors };
        var client = Client(http, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var result = await client.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        result.Outcome.Should().Be(ShipmentCreateOutcome.Failed);
        result.ErrorMessage.Should().Contain("Parcels[0].To.Contact.TelephoneNo")
            .And.Contain("The TelephoneNo field is required.");

        const string businessErrors = """{"invalid_fields":{"courier":["The Courier field is required."]},"isSuccess":false,"message":"خطا در داده های ورودی"}""";
        var businessHttp = new RecordingHandler { BulkBody = businessErrors };
        var businessClient = Client(businessHttp, new FakeCityCodeReader { Result = DeliveryCityCodeRead.Found(301) });

        var businessResult = await businessClient.CreateOrGetParcelAsync(Request("city-mashhad"), CancellationToken.None);

        businessResult.ErrorMessage.Should().Contain("courier: The Courier field is required.")
            .And.Contain("خطا در داده های ورودی");
    }

    private static PostexShipmentClient Client(
        RecordingHandler http,
        FakeCityCodeReader reader,
        double? lat = 36.92,
        double? lon = 50.65)
    {
        var options = new PostexOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            CollectionType = "postex_drop_off",
            ReadyToAccept = false,
            Origins = new Dictionary<string, PostexSenderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramsar"] = new PostexSenderOptions { Lat = lat, Lon = lon }
            }
        };
        return new PostexShipmentClient(
            new HttpClient(http) { BaseAddress = new Uri("https://postex.test/") },
            options,
            reader);
    }

    private static ShipmentCreateRequest Request(string? cityDocumentId, string? collectionType = "pick_up") =>
        new(
            new ShipmentParty(
                "علی محمدی", "09121111111", null, null, null,
                "خیابان نمونه", "مشهد", "خراسان رضوی", "1234567890", cityDocumentId),
            new ShipmentParcelDetails("BM-1", "شمش", 1, SampleParcel()),
            OriginId: "ramsar",
            CollectionType: collectionType);

    private static ParcelFacts SampleParcel() =>
        new(
            WeightGrams: 750,
            LengthCm: 15,
            WidthCm: 10,
            HeightCm: 5,
            DeclaredValueIrr: 60_000_000,
            IsFragile: true);

    private sealed class FakeCityCodeReader : IDeliveryCityCodeReader
    {
        public int Calls { get; private set; }

        public string? CityDocumentId { get; private set; }

        public string? Province { get; private set; }

        public string? City { get; private set; }

        public DeliveryCityCodeRead Result { get; init; } = DeliveryCityCodeRead.NotFound();

        public Task<DeliveryCityCodeRead> ReadAsync(
            string? cityDocumentId, string province, string city, CancellationToken ct)
        {
            Calls++;
            CityDocumentId = cityDocumentId;
            Province = province;
            City = city;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        public string? LastPostBody { get; private set; }

        public bool ExistingParcel { get; init; }

        public string? ExistingBody { get; init; }

        public HttpStatusCode? ReadStatus { get; init; }

        public HttpStatusCode BulkStatus { get; init; } = HttpStatusCode.BadRequest;

        public string? BulkBody { get; init; }

        public bool PaymentRequired { get; init; }

        private int _reads;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Paths.Add(path);
            if (request.Content is not null)
            {
                LastPostBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (path.Contains("custom-order-no", StringComparison.Ordinal))
            {
                var read = Interlocked.Increment(ref _reads);
                if (ReadStatus is HttpStatusCode status)
                {
                    return new HttpResponseMessage(status);
                }

                if (ExistingParcel)
                {
                    return Json(HttpStatusCode.OK, ExistingBody ?? """{"parcel_no":"99","tracking_number":"TRACK"}""");
                }

                if (PaymentRequired && read > 1)
                {
                    return Json(HttpStatusCode.OK,
                        """{"parcel_no":1319906676294,"parent":{"code":1},"status_code":10000,"tracking_number":"**********"}""");
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (PaymentRequired)
            {
                return Json(HttpStatusCode.PaymentRequired, """{"Message":"موجودی کافی نیست"}""");
            }

            return BulkBody is null
                ? new HttpResponseMessage(BulkStatus)
                : Json(BulkStatus, BulkBody);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
