using System.Net;
using System.Text;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class ShippingCollectionClientTests
{
    [Theory]
    [InlineData("pick_up")]
    [InlineData("courier_drop_off")]
    [InlineData("postex_drop_off")]
    public async Task ReadsTheGlobalCollectionType(string collectionType)
    {
        var handler = new RecordingHandler($$"""{"collectionType":"{{collectionType}}"}""");
        var client = Client(handler);

        var result = await client.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(ShippingCollectionStatus.Found);
        result.CollectionType.Should().Be(collectionType);
        result.CourierCode.Should().Be(ShippingDispatchCodes.DefaultCourier);
        result.ServiceType.Should().Be(ShippingDispatchCodes.DefaultServiceType);
        handler.Path.Should().Be("/api/integrations/byemoney/v1/shipping/collection-type");
        handler.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task BlankCourierAndServiceFallBackWithoutFailingTheRead()
    {
        var client = Client(new RecordingHandler(
            """{"collectionType":"pick_up","courierCode":" ","serviceType":"bad service"}"""));

        var result = await client.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(ShippingCollectionStatus.Found);
        result.CourierCode.Should().Be("IR_POST");
        result.ServiceType.Should().Be("EXPRESS");
    }

    [Fact]
    public async Task ReadsCourierAndServiceFromTheSameResponse()
    {
        var client = Client(new RecordingHandler(
            """{"collectionType":"courier_drop_off","courierCode":"chapar","serviceType":"PRIORITY"}"""));

        var result = await client.ReadAsync(CancellationToken.None);

        result.CourierCode.Should().Be("CHAPAR");
        result.ServiceType.Should().Be("PRIORITY");
    }

    [Fact]
    public async Task UnknownValueIsUnavailable()
    {
        var client = Client(new RecordingHandler("""{"collectionType":"office"}"""));

        var result = await client.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(ShippingCollectionStatus.Unavailable);
    }

    [Fact]
    public async Task HttpFailureIsUnavailable()
    {
        var client = Client(new RecordingHandler("{}", HttpStatusCode.ServiceUnavailable));

        var result = await client.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(ShippingCollectionStatus.Unavailable);
    }

    private static ShippingCollectionClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://strapi.test/") });

    private sealed class RecordingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public HttpMethod? Method { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
