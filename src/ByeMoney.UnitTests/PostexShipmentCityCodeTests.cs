using System.Net;
using System.Text;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Resources;
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

    private static PostexShipmentClient Client(RecordingHandler http, FakeCityCodeReader reader)
    {
        var options = new PostexOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            Origins = new Dictionary<string, PostexSenderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramsar"] = new PostexSenderOptions()
            }
        };
        return new PostexShipmentClient(
            new HttpClient(http) { BaseAddress = new Uri("https://postex.test/") },
            options,
            reader);
    }

    private static ShipmentCreateRequest Request(string? cityDocumentId) =>
        new(
            new ShipmentParty(
                "علی محمدی", "09121111111", null, null, null,
                "خیابان نمونه", "مشهد", "خراسان رضوی", "1234567890", cityDocumentId),
            new ShipmentParcelDetails("BM-1", "شمش", 1, 0, 0, "IRR"),
            OriginId: "ramsar");

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
                return ExistingParcel
                    ? Json(HttpStatusCode.OK, """{"parcel_no":"99","tracking_number":"TRACK"}""")
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
