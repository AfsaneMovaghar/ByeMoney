using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;
using FluentAssertions;

namespace ByeMoney.UnitTests;

/// <summary>
/// SHIP-TRACKING: کد رهگیری روی قلم کالا، ماشین وضعیت، تفسیر پاسخ پستکس و فیلتر «ارسال‌نشده».
/// </summary>
public sealed class ShipTrackingTests
{
    [Fact]
    public void MarkShippedStoresTrackingCodeAndPostexParcel()
    {
        var purchase = CreatePhysical();

        purchase.MarkShipped("  PTX-8846884  ", PhysicalProductPurchase.PostexProvider, "12345");

        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.TrackingCode.Should().Be("PTX-8846884");
        purchase.TrackingProvider.Should().Be(PhysicalProductPurchase.PostexProvider);
        purchase.ProviderParcelId.Should().Be("12345");
        purchase.ShippedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ShippedItemCanHaveItsTrackingCodeCorrectedWithoutChangingShippedAt()
    {
        var purchase = CreatePhysical();
        purchase.MarkShipped("WRONG-CODE", PhysicalProductPurchase.ManualProvider, null);
        var shippedAt = purchase.ShippedAtUtc;

        purchase.MarkShipped("RIGHT-CODE");

        purchase.TrackingCode.Should().Be("RIGHT-CODE");
        purchase.TrackingProvider.Should().Be(PhysicalProductPurchase.ManualProvider);
        purchase.ShippedAtUtc.Should().Be(shippedAt);
    }

    [Fact]
    public void TrackingIsFrozenAfterDelivery()
    {
        var purchase = CreatePhysical();
        purchase.MarkShipped("CODE", PhysicalProductPurchase.ManualProvider, null);
        purchase.MarkDelivered();

        var edit = () => purchase.MarkShipped("OTHER");
        edit.Should().Throw<DomainException>();
        purchase.TrackingCode.Should().Be("CODE");
    }

    [Fact]
    public void BlankCodeIsStoredAsNull()
    {
        var purchase = CreatePhysical();

        purchase.MarkShipped("   ", PhysicalProductPurchase.ManualProvider, null);

        purchase.TrackingCode.Should().BeNull();
    }

    [Fact]
    public void OverlongOrControlCharacterCodeIsRejected()
    {
        var purchase = CreatePhysical();

        var longCode = () => purchase.MarkShipped(new string('9', 51), PhysicalProductPurchase.ManualProvider, null);
        longCode.Should().Throw<DomainException>();

        var control = () => purchase.MarkShipped("12\n34", PhysicalProductPurchase.ManualProvider, null);
        control.Should().Throw<DomainException>();
    }

    [Fact]
    public void UnknownProviderIsRejected()
    {
        var purchase = CreatePhysical();

        var unknown = () => purchase.MarkShipped("CODE", "SomeCourier", null);
        unknown.Should().Throw<DomainException>();
    }

    [Fact]
    public void ManualProviderNeverKeepsAParcelId()
    {
        var purchase = CreatePhysical();

        purchase.MarkShipped("CODE", PhysicalProductPurchase.ManualProvider, "999");

        purchase.ProviderParcelId.Should().BeNull();
    }

    [Theory]
    [InlineData(
        "{\"parent\":{\"code\":4,\"name\":\"DELIVERED\",\"description\":\"تحویل شده\"},\"status_code\":11106,\"status_name\":\"DELIVERED\",\"status_title\":\"تحویل شده\"}",
        200)]
    [InlineData(
        "{\"parent\":{\"code\":4,\"name\":\"DELIVERED\"},\"status_code\":11202,\"status_name\":\"PACKAGE_DISTRIBUTED\",\"status_title\":\"تحویل شده\"}",
        200)]
    [InlineData("{\"parent\":{\"code\":4},\"status_code\":4,\"status_name\":\"DELIVERED\"}", 200)]
    public void PostexDeliveredStatusIsRecognised(string json, int statusCode)
    {
        PostexStatusParser.Parse(json, statusCode).Outcome
            .Should().Be(TrackingOutcome.Delivered);
    }

    [Theory]
    [InlineData(
        "{\"parent\":{\"code\":5,\"name\":\"RETURNED\"},\"status_code\":11203,\"status_name\":\"REFUSED_DISTRIBUTION\",\"status_title\":\"امتناع گیرنده\"}")]
    [InlineData(
        "{\"parent\":{\"code\":8,\"name\":\"CANCELED\"},\"status_code\":11216,\"status_name\":\"CANCELED\",\"status_title\":\"لغو ارسال\"}")]
    [InlineData("{\"parent\":{\"code\":5},\"status_code\":5,\"status_name\":\"RETURNED\"}")]
    public void PostexReturnedStatusIsRecognised(string json)
    {
        PostexStatusParser.Parse(json, 200).Outcome
            .Should().Be(TrackingOutcome.Returned);
    }

    [Fact]
    public void PostexRealPaymentPendingShapeIsInTransit()
    {
        const string json =
            "{\"parent\":{\"code\":1,\"name\":\"PAYMENT_PENDING\",\"description\":\"پرداخت مرسوله هنوز انجام نشده است\"}," +
            "\"parcel_no\":1319906676294,\"tracking_number\":\"**********\",\"status_code\":10000," +
            "\"status_name\":\"PAYMENT_PENDING\",\"status_title\":\"در انتظار پرداخت\"," +
            "\"status_description\":\"پرداخت مرسوله هنوز انجام نشده است\",\"latest_status_changed\":null," +
            "\"courier\":{\"name\":\"IR_POST\",\"service\":\"EXPRESS\"}}";

        var result = PostexStatusParser.Parse(json, 200);

        result.Outcome.Should().Be(TrackingOutcome.InTransit);
        result.StatusName.Should().Be("PAYMENT_PENDING");
    }

    [Fact]
    public void PostexMisparentedExceptionCodesStayInTransit()
    {
        PostexStatusParser.Parse(
            "{\"parent\":{\"code\":4,\"name\":\"DELIVERED\"},\"status_code\":11311,\"status_name\":\"DELIVERY_RETRY\"}",
            200).Outcome.Should().Be(TrackingOutcome.InTransit);

        PostexStatusParser.Parse(
            "{\"parent\":{\"code\":8,\"name\":\"CANCELED\"},\"status_code\":11310,\"status_name\":\"ROUTE_NOT_FOUND\"}",
            200).Outcome.Should().Be(TrackingOutcome.InTransit);
    }

    [Fact]
    public void PostexInTransitAndErrorsAreNotDelivered()
    {
        PostexStatusParser.Parse(
            "{\"parent\":{\"code\":3,\"name\":\"IN_TRANSIT\"},\"status_code\":11208,\"status_name\":\"SHIPPED\"}",
            200).Outcome.Should().Be(TrackingOutcome.InTransit);
        PostexStatusParser.Parse("{}", 404).Outcome
            .Should().Be(TrackingOutcome.NotFound);
        PostexStatusParser.Parse("{\"Message\":\"Parcel 999 not found\"}", 400).Outcome
            .Should().Be(TrackingOutcome.NotFound);
        PostexStatusParser.Parse("{}", 500).Outcome
            .Should().Be(TrackingOutcome.Failed);
        PostexStatusParser.Parse("not-json", 200).Outcome
            .Should().Be(TrackingOutcome.Failed);
        // نام متنی بدون کد عددی دیگر مبنای تحویل نیست.
        PostexStatusParser.Parse("{\"status_name\":\"DELIVERED\",\"status_title\":\"تحویل شده\"}", 200).Outcome
            .Should().Be(TrackingOutcome.Failed);
    }

    [Fact]
    public void AwaitingShipmentKeepsOnlyPaidOrdersWithAnUnshippedProduct()
    {
        var buyer = Guid.NewGuid();
        var rows = new[]
        {
            Row(buyer, paid: true, quantity: 2, cancelled: 0, delivery: "Preparing"),
            Row(buyer, paid: true, quantity: 1, cancelled: 0, delivery: "Shipped"),
            Row(buyer, paid: true, quantity: 1, cancelled: 1, delivery: "Preparing"),
            Row(buyer, paid: false, quantity: 1, cancelled: 0, delivery: "Preparing"),
        };

        var page = OrderPaging.Page(rows, new OrderFilter(PageSize: 20), awaitingShipment: true);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Details.FinancialStatus.Should().Be("Paid");
        page.Items.Single().Details.Items.Single().PhysicalDeliveryStatus.Should().Be("Preparing");
    }

    private static PhysicalProductPurchase CreatePhysical() => PhysicalProductPurchase.Create(
        PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
        "product-1", "Book", 2, 50m, 100m);

    private static OrderReadRow Row(
        Guid buyerId, bool paid, int quantity, int cancelled, string delivery) =>
        new(buyerId, new OrderDetails(
            $"cart:{Guid.NewGuid():D}", "Cart", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            paid ? "Paid" : "Processing", "Pending", 100m, 1, null, null, null, null,
            [
                new OrderItem("product-1", "Book", 50m, 100m, Guid.NewGuid(), null, DateTime.UtcNow,
                    paid ? "Paid" : "Processing", "Pending", "Product", null, quantity, delivery, cancelled)
            ],
            [], null));
}
