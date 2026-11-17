using System.Net;
using System.Text;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class DeliveryCityCodeClientTests
{
    [Fact]
    public async Task ReadsCarrierCodeByCityDocumentIdOnly()
    {
        var handler = new RecordingHandler("""{"carrierCityCode":301}""");
        var client = Client(handler);

        var result = await client.ReadAsync(" city-mashhad ", "خراسان رضوی", "مشهد", CancellationToken.None);

        result.Status.Should().Be(DeliveryCityCodeStatus.Found);
        result.Code.Should().Be(301);
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("cityDocumentId").GetString().Should().Be("city-mashhad");
        body.RootElement.TryGetProperty("province", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("city", out _).Should().BeFalse();
        handler.Path.Should().Be("/api/integrations/byemoney/v1/localities/carrier-code");
    }

    [Fact]
    public async Task ReadsLegacyNamePairWhenDocumentIdIsAbsent()
    {
        var handler = new RecordingHandler("""{"carrierCityCode":573}""");
        var client = Client(handler);

        var result = await client.ReadAsync(null, " مازندران ", " رامسر ", CancellationToken.None);

        result.Code.Should().Be(573);
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("province").GetString().Should().Be("مازندران");
        body.RootElement.GetProperty("city").GetString().Should().Be("رامسر");
        body.RootElement.TryGetProperty("cityDocumentId", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task MissingCityDoesNotInventACode(HttpStatusCode status)
    {
        var client = Client(new RecordingHandler("{}", status));

        var result = await client.ReadAsync("missing", "تهران", "تهران", CancellationToken.None);

        result.Status.Should().Be(DeliveryCityCodeStatus.NotFound);
        result.Code.Should().BeNull();
    }

    [Fact]
    public async Task TransportFailureIsUnavailable()
    {
        var client = Client(new ThrowingHandler());

        var result = await client.ReadAsync("city-mashhad", "خراسان رضوی", "مشهد", CancellationToken.None);

        result.Status.Should().Be(DeliveryCityCodeStatus.Unavailable);
    }

    private static DeliveryCityCodeClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://strapi.test/") });

    private sealed class RecordingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? Path { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("down");
    }
}
