using System.Net;
using System.Text;
using System.Text.Json;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;
using FluentAssertions;

namespace ByeMoney.UnitTests;

/// <summary>
/// آداپتور طرح الهی: نام عام قلم به قرارداد ثابت <c>carts/prepare</c> استرپی نگاشت می‌شود و
/// قاعدهٔ مخصوص کاتالوگ (والد فصل، تعداد دوره) فقط همین‌جا بررسی می‌شود.
/// </summary>
public sealed class CartPricingClientTests
{
    private static readonly Guid CartId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task CourseAndChapterOnlyCartUsesLegacyExternalCourseIdsShape()
    {
        var handler = new CapturingHandler(QuoteJson("""{"externalCourseId":"b","type":"course","title":"ب","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}"""));
        var client = Client(handler);

        await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null,
            [new CartItemInput("b"), new CartItemInput("a", ProductItemType.CourseChapter)]), default);

        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("externalCourseIds").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("a", "b");
        body.RootElement.TryGetProperty("items", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ProductCartUsesTypedItemsShape()
    {
        var handler = new CapturingHandler(QuoteJson("""{"externalCourseId":"p","type":"product","title":"کالا","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":30,"quantity":3}""", FullAddressJson));
        var client = Client(handler);

        var result = await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null,
            [new CartItemInput("p", ProductItemType.Product, 3)]), default);

        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.TryGetProperty("externalCourseIds", out _).Should().BeFalse();
        var item = body.RootElement.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("externalId").GetString().Should().Be("p");
        item.GetProperty("type").GetString().Should().Be("product");
        item.GetProperty("quantity").GetInt32().Should().Be(3);
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().ExternalItemId.Should().Be("p");
        result.Value.Items.Single().Quantity.Should().Be(3);
    }

    [Theory]
    [InlineData("""{"externalCourseId":"c","type":"course_chapter","title":"فصل","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""")]
    [InlineData("""{"externalCourseId":"c","type":"course","parentExternalId":"parent","title":"دوره","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""")]
    [InlineData("""{"externalCourseId":"c","type":"course","quantity":2,"title":"دوره","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":20}""")]
    [InlineData("""{"externalCourseId":"c","type":"product","parentExternalId":"parent","title":"کالا","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""")]
    [InlineData("""{"type":"course","title":"دوره","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""")]
    public async Task QuoteViolatingTarhElahiCatalogShapeIsInvalid(string itemJson)
    {
        var client = Client(new CapturingHandler(QuoteJson(itemJson)));

        var result = await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null, [new CartItemInput("c")]), default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CartErrorCodes.Invalid);
    }

    private const string ProductItemJson = """{"externalCourseId":"p","type":"product","title":"Book","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""";
    private const string FullAddressJson = """ "addressSnapshot":{"province":"P","city":"C","postalCode":null,"fullAddress":"Street 1","recipientName":"R","recipientPhone":"09120000000"},"addressVersion":"abc123" """;

    [Fact]
    public async Task ProductQuoteCarriesAddressSnapshotAndVersion()
    {
        var client = Client(new CapturingHandler(QuoteJson(ProductItemJson, FullAddressJson)));

        var result = await client.PrepareAsync(
            new CartQuoteRequest(CartId, "buyer", null, [new CartItemInput("p", ProductItemType.Product, 1)]), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShippingAddress.Should().Be(
            new ShippingAddress("abc123", "P", "C", null, "Street 1", "R", "09120000000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(""" "addressSnapshot":null,"addressVersion":null """)]
    [InlineData(""" "addressSnapshot":{"province":"P","city":"C","fullAddress":"Street 1","recipientName":"R","recipientPhone":"09120000000"},"addressVersion":null """)]
    [InlineData(""" "addressSnapshot":{"province":"P","city":" ","fullAddress":"Street 1","recipientName":"R","recipientPhone":"09120000000"},"addressVersion":"abc123" """)]
    [InlineData(""" "addressSnapshot":{"province":"P","city":"C","fullAddress":"Street 1","recipientName":"R"},"addressVersion":"abc123" """)]
    public async Task ProductQuoteWithoutCompleteAddressAndVersionIsInvalid(string addressJson)
    {
        var client = Client(new CapturingHandler(QuoteJson(ProductItemJson, addressJson)));

        var result = await client.PrepareAsync(
            new CartQuoteRequest(CartId, "buyer", null, [new CartItemInput("p", ProductItemType.Product, 1)]), default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(CartErrorCodes.Invalid);
    }

    [Fact]
    public async Task CourseOnlyQuoteIgnoresAddressSnapshot()
    {
        const string courseJson = """{"externalCourseId":"c","type":"course","title":"Course","basePriceInNoor":10,"itemDiscountInNoor":0,"couponDiscountInNoor":0,"priceInNoor":10}""";
        var client = Client(new CapturingHandler(QuoteJson(courseJson, FullAddressJson)));

        var result = await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null, [new CartItemInput("c")]), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShippingAddress.Should().BeNull();
    }

    [Fact]
    public void DomainAcceptsGenericParentButRejectsSelfParent()
    {
        var cart = Cart.Create(Guid.NewGuid(), Guid.NewGuid(), "buyer", [new CartItemInput("x", ProductItemType.Product)], null);
        var selfParent = new CartQuoteItem("x", "کالا", 10, 0, 0, 10, ProductItemType.Product, "x");

        var act = () => cart.SetQuote([selfParent]);

        act.Should().Throw<ArgumentException>();
        cart.SetQuote([selfParent with { ParentExternalId = "bundle" }]);
        cart.Items.Single().ParentExternalId.Should().Be("bundle");
    }

    [Fact]
    public async Task PrepareStockLineErrorKeepsItemReasonAndAvailableQuantity()
    {
        const string body = """{"code":"CART_UNAVAILABLE","error":"x","lineError":{"externalId":"p","type":"product","quantity":3,"reason":"INSUFFICIENT_STOCK","availableQuantity":1}}""";
        var client = Client(new CapturingHandler(body, HttpStatusCode.Conflict));

        var result = await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null,
            [new CartItemInput("p", ProductItemType.Product, 3)]), default);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(CartErrorCodes.StockUnavailable);
        result.ErrorMessage.Should().Be(CartErrors.InsufficientStock(1));
        ((ICartStockFailure)result).LineError.Should().Be(new CartLineError("p", CartStockReasons.InsufficientStock, 1));
    }

    [Fact]
    public async Task CheckOutOfStockLineErrorIsStockFailure()
    {
        const string body = """{"code":"CART_UNAVAILABLE","error":"x","lineError":{"externalId":"p","type":"product","quantity":1,"reason":"OUT_OF_STOCK","availableQuantity":0}}""";
        var client = Client(new CapturingHandler(body, HttpStatusCode.Conflict));

        var result = await client.TransitionAsync(new CartTransitionRequest(CartId, "buyer"), "check", default);

        result.ErrorCode.Should().Be(CartErrorCodes.StockUnavailable);
        result.ErrorMessage.Should().Be(CartErrors.OutOfStock);
        ((ICartStockFailure)result).LineError.Should().Be(new CartLineError("p", CartStockReasons.OutOfStock, 0));
    }

    [Theory]
    [InlineData("""{"code":"CART_CONFLICT","error":"x"}""")]
    [InlineData("""{"code":"CART_UNAVAILABLE","error":"x","lineError":{"externalId":"c","type":"course","quantity":1,"reason":"ALREADY_OWNED"}}""")]
    [InlineData("""{"code":"CART_UNAVAILABLE","error":"x","lineError":{"externalId":"p","reason":"INSUFFICIENT_STOCK"}}""")]
    [InlineData("""{"code":"CART_UNAVAILABLE","error":"x","lineError":{"reason":"OUT_OF_STOCK","availableQuantity":0}}""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task OtherConflictsStayGenericConflict(string body)
    {
        var client = Client(new CapturingHandler(body, HttpStatusCode.Conflict));

        var result = await client.PrepareAsync(new CartQuoteRequest(CartId, "buyer", null, [new CartItemInput("c")]), default);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(CartErrorCodes.Conflict);
        result.Should().NotBeAssignableTo<ICartStockFailure>();
    }

    private static CartPricingClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://strapi.test/") });

    private static string QuoteJson(string itemJson, string extraJson = "")
    {
        var extra = string.IsNullOrEmpty(extraJson) ? string.Empty : "," + extraJson;
        return $$"""{"cartId":"{{CartId}}","buyerExternalUserId":"buyer","couponCode":null,"items":[{{itemJson}}]""" + extra + "}";
    }

    private sealed class CapturingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
