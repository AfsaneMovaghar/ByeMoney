using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class CreatePhysicalShipmentTests
{
    [Fact]
    public async Task CreatesParcelAndMarksShippedThroughProviderPort()
    {
        var purchase = CreatePreparing();
        var address = SampleAddress();
        var store = new FakeDeliveryStore(purchase, address);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.Created, "1319906676294", "TRACK-1", "PAYMENT_PENDING")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository, collectionTypes: new FixedCollection("pick_up"))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.ProviderParcelId.Should().Be("1319906676294");
        response.TrackingCode.Should().Be("TRACK-1");
        response.TrackingProvider.Should().Be(PhysicalProductPurchase.PostexProvider);
        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.Created));
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        client.LastRequest!.Parcel.IdempotencyKey.Should().Be($"BM-{purchase.CartId:N}");
        var item = client.LastRequest.Parcel.Items.Should().ContainSingle().Subject;
        item.Quantity.Should().Be(1);
        item.Description.Should().Be("شمش");
        item.WeightGrams.Should().Be(750);
        item.ValueIrr.Should().Be(60_000_000);
        client.LastRequest.Parcel.BoxTypeId.Should().Be(5);
        client.LastRequest.Parcel.LengthCm.Should().Be(15);
        client.LastRequest.Parcel.WidthCm.Should().Be(10);
        client.LastRequest.Parcel.HeightCm.Should().Be(20);
        client.LastRequest.Parcel.TotalWeightGrams.Should().Be(750);
        client.LastRequest.Parcel.TotalValueIrr.Should().Be(60_000_000);
        client.LastRequest.Parcel.IsFragile.Should().BeTrue();
        client.LastRequest.Recipient.CityName.Should().Be("مشهد");
        client.LastRequest.Recipient.CityDocumentId.Should().Be("city-mashhad");
        client.LastRequest.OriginId.Should().Be("ramsar");
        client.LastRequest.CollectionType.Should().Be("pick_up");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("**********")]
    [InlineData("****1234")]
    public async Task ParcelReferenceWithoutPostalCodeStaysPreparing(string? trackingCode)
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1319906676294", trackingCode)
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Preparing));
        response.Warning.Should().Be(OrderErrors.ShipmentTrackingCodePending);
        response.Outcome.Should().Be("AwaitingTrackingCode");
        response.ProviderParcelId.Should().Be("1319906676294");
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.ProviderParcelId.Should().Be("1319906676294");
        purchase.TrackingCode.Should().BeNull();
        purchase.ShippedAtUtc.Should().BeNull();
        client.LastRequest!.CollectionType.Should().Be("courier_drop_off");
    }

    [Fact]
    public async Task UsesTheStrapiCollectionTypeAndIgnoresAnyRequestOverride()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1", "IR-1")
        };

        await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository, collectionTypes: new FixedCollection("postex_drop_off"))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        client.LastRequest!.CollectionType.Should().Be("postex_drop_off");
        client.LastRequest.CourierCode.Should().Be("IR_POST");
        client.LastRequest.ServiceType.Should().Be("EXPRESS");
    }

    [Fact]
    public async Task UsesCourierAndServiceFromStrapiAndFallsBackWhenTheyAreBlank()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1", "IR-1")
        };

        await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository,
            collectionTypes: new FixedCollection("pick_up", "chapar", " "))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        client.LastRequest!.CourierCode.Should().Be("CHAPAR");
        client.LastRequest.ServiceType.Should().Be("EXPRESS");
    }

    [Fact]
    public async Task UnavailableCollectionSettingDoesNotCallTheProvider()
    {
        var purchase = CreatePreparing();
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);
        var act = () => new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(purchase), new FakeDeliveryStore(purchase, SampleAddress()), [client],
            new FakeRepository(purchase), collectionTypes: new FixedCollection(null))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentCollectionTypeUnavailable);
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task LaterRealCodeShipsTheSameParcelWithoutDeliveringIt()
    {
        var purchase = CreatePreparing();
        purchase.RememberCarrierParcel(
            PhysicalProductPurchase.PostexProvider, "1319906676294", purchase.RemainingQuantity);
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.AlreadyExists, "1319906676294", "IR-POST-100")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.TrackingCode.Should().Be("IR-POST-100");
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.DeliveredAtUtc.Should().BeNull();
        purchase.ProviderParcelId.Should().Be("1319906676294");
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ForwardsLegacyAddressWithoutCityDocumentId()
    {
        var purchase = CreatePreparing();
        var address = new ShippingAddress(
            "ver", "خراسان رضوی", "مشهد", null, "خیابان نمونه", "علی محمدی", "09121111111");
        var store = new FakeDeliveryStore(purchase, address);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1")
        };

        await new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        client.Calls.Should().Be(1);
        client.LastRequest!.Recipient.CityDocumentId.Should().BeNull();
        client.LastRequest.Recipient.ProvinceName.Should().Be("خراسان رضوی");
        client.LastRequest.Recipient.CityName.Should().Be("مشهد");
    }

    [Fact]
    public async Task RejectsWhenCartOriginIsMissing()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress(), originId: null);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsWhenFrozenOriginCityDiffersBeforeAnyProviderCall()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress(), originCityId: 259);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository, new FixedOriginCity(573))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentOriginCityMismatch);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MatchingFrozenOriginCityStillCreatesTheParcel()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress(), originCityId: 573);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1", "TRACK-1")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository, new FixedOriginCity(573))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.Created));
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RejectsLegacyPurchaseWithoutParcelFacts()
    {
        var purchase = CreatePreparingWithoutParcel();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentParcelRequired);
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsWhenProviderIsNotConfigured()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider) { IsConfigured = false };

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsWhenAnActiveItemIsAlreadyShipped()
    {
        var purchase = CreatePreparing();
        purchase.MarkShipped("OLD", PhysicalProductPurchase.PostexProvider, "111");
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentAlreadyDispatched);
        client.Calls.Should().Be(0);
        purchase.ProviderParcelId.Should().Be("111");
    }

    [Fact]
    public async Task PendingPaymentParcelIsStoredAndStaysPreparing()
    {
        var purchase = CreatePreparing();
        var paidAmount = purchase.PaidAmountInNoor;
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.PendingPayment, "99", ErrorMessage: OrderErrors.ShipmentPaymentPending)
        };

        var response = await new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        // مرسولهٔ پرداخت‌نشده نه خطاست و نه ارسال؛ شناسه و نشان انتظار ذخیره می‌شود.
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Preparing));
        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.PendingPayment));
        response.ProviderParcelId.Should().Be("99");
        response.CarrierPaymentPendingSinceUtc.Should().NotBeNull();
        response.Warning.Should().Be(OrderErrors.ShipmentPaymentPendingHelp);

        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.ProviderParcelId.Should().Be("99");
        purchase.HasPendingCarrierPayment.Should().BeTrue();
        purchase.CarrierParcelQuantity.Should().Be(1);
        purchase.ShipmentRequestStartedAtUtc.Should().BeNull();

        // بی‌اثر مالی: مبلغ پرداخت و برگشتی‌ها دست نمی‌خورند و ارسال‌شده هم نیست.
        purchase.PaidAmountInNoor.Should().Be(paidAmount);
        purchase.RefundedRegularInNoor.Should().Be(0m);
        purchase.RefundedGiftInNoor.Should().Be(0m);
        purchase.ShippedAtUtc.Should().BeNull();
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SecondCreateWhileFirstIsInFlightIsRejectedWithoutProviderCall()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "99", "TRACK-1"),
            BeforeResult = _ =>
            {
                entered.TrySetResult();
                return release.Task;
            }
        };
        var handler = new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository);
        var command = new CreatePhysicalShipmentCommand(purchase.CartId);

        var first = handler.Handle(command, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // رزرو کوتاه در دیتابیس تا پایان فراخوانی واسط باقی می‌ماند.
        var act = () => handler.Handle(command, CancellationToken.None);
        (await act.Should().ThrowAsync<ConflictDomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentRequestInProgress);
        client.Calls.Should().Be(1);

        release.TrySetResult();
        await first;

        client.Calls.Should().Be(1);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.ProviderParcelId.Should().Be("99");
        purchase.ShipmentRequestStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task UncertainOutcomeKeepsTheReservationForTheNextAttempt()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.Uncertain, ErrorMessage: OrderErrors.ShipmentOutcomeUnknown)
        };

        var act = () => new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentOutcomeUnknown);

        // نتیجهٔ نامعلوم رزرو را آزاد نمی‌کند؛ تلاش بعدی باید اول بخواند و مرسولهٔ دوم نسازد.
        purchase.ShipmentRequestStartedAtUtc.Should().NotBeNull();
        purchase.ProviderParcelId.Should().BeNull();
    }

    [Fact]
    public async Task ExplicitProviderRejectionReleasesTheReservation()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Failed, ErrorMessage: "رد شد")
        };

        var handler = new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository);
        var command = new CreatePhysicalShipmentCommand(purchase.CartId);

        var act = () => handler.Handle(command, CancellationToken.None);
        (await act.Should().ThrowAsync<DomainException>()).Which.Message.Should().Be("رد شد");
        purchase.ShipmentRequestStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CancellationDuringProviderCallStillStoresTheParcelReference()
    {
        var purchase = CreatePreparing();
        purchase.RecordPaymentShares(new PaymentShares(100m, 0m));
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.PendingPayment, "99", ErrorMessage: OrderErrors.ShipmentPaymentPending),
            BeforeResult = _ =>
            {
                // لغو کامل در همان فاصلهٔ فراخوانی واسط؛ پول برمی‌گردد و قلم چیزی برای ارسال ندارد.
                purchase.RecordCancellation(1, new PaymentShares(100m, 0m));
                return Task.CompletedTask;
            }
        };

        var response = await new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Preparing));
        response.Warning.Should().Be(OrderErrors.ShipmentQuantityMismatch);
        purchase.RemainingQuantity.Should().Be(0);
        purchase.ProviderParcelId.Should().Be("99");
        purchase.HasPendingCarrierPayment.Should().BeTrue();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.RefundedRegularInNoor.Should().Be(100m);
        purchase.ShippedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AggregatesActiveCartItemsWithScaledFactsAndCartIdempotencyKey()
    {
        var cartId = Guid.NewGuid();
        var buyer = new UserId(Guid.NewGuid());
        var gold = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "gold", "شمش", 2, 100m, 200m, FinancialScope.TarheElahi, new ParcelFacts(1000, 99, 99, 99, 80_000_000, true, 4));
        gold.RecordPaymentShares(new PaymentShares(200m, 0m));
        gold.RecordCancellation(1, new PaymentShares(100m, 0m));
        var coin = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "coin", "سکه", 1, 50m, 50m, FinancialScope.TarheElahi, new ParcelFacts(250, 15, 10, 20, 10_000_000, false, 6));
        var cancelled = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "old", "لغوشده", 1, 10m, 10m, FinancialScope.TarheElahi, new ParcelFacts(100, 10, 10, 15, 1_000_000, false, 4));
        cancelled.RecordPaymentShares(new PaymentShares(10m, 0m));
        cancelled.RecordCancellation(1, new PaymentShares(10m, 0m));

        var store = new FakeDeliveryStore([gold, coin, cancelled], SampleAddress());
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "555", "IR-9")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(gold), store, [client], new FakeRepository(gold))
            .Handle(new CreatePhysicalShipmentCommand(cartId), CancellationToken.None);

        response.CartId.Should().Be(cartId);
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.Created));
        client.LastRequest!.Parcel.IdempotencyKey.Should().Be($"BM-{cartId:N}");
        client.LastRequest.Parcel.BoxTypeId.Should().Be(7);
        client.LastRequest.Parcel.LengthCm.Should().Be(20);
        client.LastRequest.Parcel.WidthCm.Should().Be(20);
        client.LastRequest.Parcel.HeightCm.Should().Be(30);
        client.LastRequest.Parcel.IsFragile.Should().BeTrue();
        client.LastRequest.Parcel.TotalWeightGrams.Should().Be(750);
        client.LastRequest.Parcel.TotalValueIrr.Should().Be(50_000_000);
        client.LastRequest.Parcel.Items.Should().BeEquivalentTo(
        [
            new ShipmentParcelItem("شمش", 1, 500, 40_000_000),
            new ShipmentParcelItem("سکه", 1, 250, 10_000_000)
        ]);

        gold.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        coin.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        gold.ProviderParcelId.Should().Be("555");
        coin.ProviderParcelId.Should().Be("555");
        gold.TrackingCode.Should().Be("IR-9");
        coin.TrackingCode.Should().Be("IR-9");
        gold.TrackingProvider.Should().Be(PhysicalProductPurchase.PostexProvider);
        coin.ShippedAtUtc.Should().NotBeNull();
        cancelled.ProviderParcelId.Should().BeNull();
        cancelled.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task ManualBoxTypeOverridesTheLargestItemBox()
    {
        var cartId = Guid.NewGuid();
        var buyer = new UserId(Guid.NewGuid());
        var first = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "a", "الف", 1, 10m, 10m, FinancialScope.TarheElahi, new ParcelFacts(100, 10, 10, 15, 1_000_000, false, 4));
        var second = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "b", "ب", 1, 10m, 10m, FinancialScope.TarheElahi, new ParcelFacts(100, 15, 10, 20, 1_000_000, false, 6));
        var store = new FakeDeliveryStore([first, second], SampleAddress());
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1", "TRACK-1")
        };

        await new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(first), store, [client], new FakeRepository(first))
            .Handle(new CreatePhysicalShipmentCommand(cartId, BoxTypeId: 8), CancellationToken.None);

        client.LastRequest!.Parcel.BoxTypeId.Should().Be(8);
        client.LastRequest.Parcel.LengthCm.Should().Be(25);
        client.LastRequest.Parcel.WidthCm.Should().Be(25);
        client.LastRequest.Parcel.HeightCm.Should().Be(35);
    }

    [Fact]
    public async Task StepsBoxSevenUpWhenTheCartHasTwoUnits()
    {
        var cartId = Guid.NewGuid();
        var buyer = new UserId(Guid.NewGuid());
        var first = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "a", "الف", 1, 10m, 10m, FinancialScope.TarheElahi, new ParcelFacts(100, 20, 20, 30, 1_000_000, false, 7));
        var second = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "b", "ب", 1, 10m, 10m, FinancialScope.TarheElahi, new ParcelFacts(100, 20, 20, 30, 1_000_000, false, 7));
        var store = new FakeDeliveryStore([first, second], SampleAddress());
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1", "TRACK-1")
        };

        await new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(first), store, [client], new FakeRepository(first))
            .Handle(new CreatePhysicalShipmentCommand(cartId), CancellationToken.None);

        client.LastRequest!.Parcel.BoxTypeId.Should().Be(8);
        client.LastRequest.Parcel.LengthCm.Should().Be(25);
        client.LastRequest.Parcel.WidthCm.Should().Be(25);
        client.LastRequest.Parcel.HeightCm.Should().Be(35);
    }

    [Theory]
    [InlineData(7, 1, 7)]
    [InlineData(12, 3, 12)]
    [InlineData(2, 4, 2)]
    [InlineData(3, 2, 3)]
    public void ShipmentBoxRuleMatchesTheOwnerDecision(int baseBox, int units, int expected)
    {
        PostexStandardBoxes.ShipmentBoxTypeId(baseBox, units, null).Should().Be(expected);
    }

    [Fact]
    public void ManualBoxTypeIsNotSteppedUp()
    {
        PostexStandardBoxes.ShipmentBoxTypeId(7, 4, 7).Should().Be(7);
    }

    [Fact]
    public async Task RejectsWhenAnyActiveItemLacksParcelFactsOrWasDelivered()
    {
        var cartId = Guid.NewGuid();
        var buyer = new UserId(Guid.NewGuid());
        var ready = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "a", "الف", 1, 10m, 10m, FinancialScope.TarheElahi, SampleParcel());
        var missing = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "b", "ب", 1, 10m, 10m, FinancialScope.TarheElahi);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var missingFacts = () => new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(ready), new FakeDeliveryStore([ready, missing], SampleAddress()), [client],
            new FakeRepository(ready))
            .Handle(new CreatePhysicalShipmentCommand(cartId), CancellationToken.None);

        (await missingFacts.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentParcelRequired);

        var delivered = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cartId, buyer, "c", "ج", 1, 10m, 10m, FinancialScope.TarheElahi, SampleParcel());
        delivered.MarkShipped("CODE", PhysicalProductPurchase.PostexProvider, "9");
        delivered.MarkDelivered();
        var deliveredAct = () => new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(ready), new FakeDeliveryStore([ready, delivered], SampleAddress()), [client],
            new FakeRepository(ready))
            .Handle(new CreatePhysicalShipmentCommand(cartId), CancellationToken.None);

        (await deliveredAct.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentAlreadyDispatched);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RejectsBoxTypeOutsideTheStandardTableBeforeCallingTheProvider()
    {
        var purchase = CreatePreparing();
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);
        var act = () => new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(purchase), new FakeDeliveryStore(purchase, SampleAddress()), [client],
            new FakeRepository(purchase))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId, BoxTypeId: 13), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.InvalidBoxTypeId);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RejectsCartWithNoRemainingPhysicalItems()
    {
        var purchase = CreatePreparing();
        purchase.RecordPaymentShares(new PaymentShares(100m, 0m));
        purchase.RecordCancellation(1, new PaymentShares(100m, 0m));
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);
        var act = () => new CreatePhysicalShipmentCommandHandler(
            new FakeRepository(purchase), new FakeDeliveryStore(purchase, SampleAddress()), [client],
            new FakeRepository(purchase))
            .Handle(new CreatePhysicalShipmentCommand(purchase.CartId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentNoActiveItems);
        client.Calls.Should().Be(0);
    }

    private static PhysicalProductPurchase CreatePreparing() =>
        CreatePreparing(SampleParcel());

    private static PhysicalProductPurchase CreatePreparingWithoutParcel() =>
        CreatePreparing(null);

    private static PhysicalProductPurchase CreatePreparing(ParcelFacts? parcel) =>
        PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()), "product-1", "شمش", 1, 100m, 100m, FinancialScope.TarheElahi, parcel);

    private static ParcelFacts SampleParcel() =>
        new(
            WeightGrams: 750,
            LengthCm: 15,
            WidthCm: 10,
            HeightCm: 20,
            DeclaredValueIrr: 60_000_000,
            IsFragile: true,
            BoxTypeId: 5);

    private static ShippingAddress SampleAddress() =>
        new("ver", "خراسان رضوی", "مشهد", "1234567890", "خیابان نمونه", "علی محمدی", "09121111111", "city-mashhad");

    private sealed class FakeShipmentClient(string provider) : IShipmentProviderClient
    {
        public string Provider => provider;

        public bool IsConfigured { get; init; } = true;

        private int _calls;

        public int Calls => _calls;

        public ShipmentCreateRequest? LastRequest { get; private set; }

        public ShipmentCreateResult Result { get; init; } =
            new(ShipmentCreateOutcome.Failed, ErrorMessage: "not set");

        public Func<CancellationToken, Task>? BeforeResult { get; init; }

        public ShipmentCreateResult ReadResult { get; init; } =
            new(ShipmentCreateOutcome.NotFound);

        public async Task<ShipmentCreateResult> CreateOrGetParcelAsync(
            ShipmentCreateRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            LastRequest = request;
            if (BeforeResult is not null)
            {
                await BeforeResult(ct);
            }

            return Result;
        }

        public Task<ShipmentCreateResult> GetParcelAsync(string idempotencyKey, CancellationToken ct) =>
            Task.FromResult(ReadResult);
    }

    private sealed class FixedCollection(string? collectionType, string? courier = null, string? service = null)
        : IShippingCollectionReader
    {
        public Task<ShippingCollectionRead> ReadAsync(CancellationToken ct) =>
            Task.FromResult(collectionType is null
                ? ShippingCollectionRead.Unavailable()
                : ShippingCollectionRead.Found(collectionType, courier, service));
    }

    private sealed class FixedOriginCity(int? cityId) : IShippingOriginCityReader
    {
        public int? GetCityId(string? shippingOriginId) => cityId;
    }

    private sealed class FakeDeliveryStore : IPhysicalDeliveryStore
    {
        private readonly IReadOnlyList<PhysicalProductPurchase> _purchases;
        private readonly ShippingAddress? _address;
        private readonly string? _originId;
        private readonly int? _originCityId;

        public FakeDeliveryStore(
            PhysicalProductPurchase purchase,
            ShippingAddress? address,
            string? originId = "ramsar",
            int? originCityId = null)
            : this([purchase], address, originId, originCityId)
        {
        }

        public FakeDeliveryStore(
            IReadOnlyList<PhysicalProductPurchase> purchases,
            ShippingAddress? address,
            string? originId = "ramsar",
            int? originCityId = null)
        {
            _purchases = purchases;
            _address = address;
            _originId = originId;
            _originCityId = originCityId;
        }

        public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct)
        {
            var purchase = _purchases.FirstOrDefault(item => item.Id.Value == purchaseId);
            return Task.FromResult(purchase is null
                ? null
                : new PhysicalPurchaseOwner(purchase.BuyerId.Value, purchase.CartId));
        }

        public Task<PhysicalPurchaseOwner?> FindCartOwnerAsync(Guid cartId, CancellationToken ct)
        {
            var purchase = _purchases.FirstOrDefault(item => item.CartId == cartId);
            return Task.FromResult(purchase is null
                ? null
                : new PhysicalPurchaseOwner(purchase.BuyerId.Value, purchase.CartId));
        }

        public Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(
            Guid cartId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalProductPurchase>>(
                _purchases.Where(item => item.CartId == cartId).ToList());

        public Task<ShippingAddress?> GetCartShippingAddressAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult(_address);

        public Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult(_originId);

        public Task<int?> GetCartShippingOriginCityIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult(_originCityId);

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
            DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit,
            bool excludeOpenCarrierAlerts, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
            IReadOnlyCollection<string> providers, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return await work();
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private sealed class FakeRepository(PhysicalProductPurchase purchase) :
        IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>,
        IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);

        public Task<PhysicalProductPurchase?> GetByIdAsync(
            PhysicalProductPurchaseId id, CancellationToken ct = default) =>
            Task.FromResult(id.Value == purchase.Id.Value ? purchase : null);

        public Task<List<PhysicalProductPurchase>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<PhysicalProductPurchase> { purchase });

        public Task AddAsync(PhysicalProductPurchase entity, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Update(PhysicalProductPurchase entity) { }

        public void Remove(PhysicalProductPurchase entity) { }
    }
}
