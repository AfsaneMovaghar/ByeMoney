using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// ساخت یک مرسوله برای همهٔ اقلام فیزیکیِ فعال یک سبد، از طریق پورت عمومی
/// <see cref="IShipmentProviderClient"/>. فراخوانی واسط بیرون از تراکنش و قفل است:
/// اول رزرو کوتاه روی همهٔ اقلام فعال، بعد HTTP، و در پایان نتیجه در یک تراکنش
/// روی همان اقلام ذخیره می‌شود تا از هم جدا نمانند.
/// </summary>
public sealed class CreatePhysicalShipmentCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IEnumerable<IShipmentProviderClient> shipmentClients,
    IUnitOfWork unitOfWork,
    IShippingOriginCityReader? originCities = null,
    IShippingCollectionReader? collectionTypes = null) :
    IRequestHandler<CreatePhysicalShipmentCommand, CreatePhysicalShipmentResponse>
{
    /// <summary>
    /// مهلت رزرو ساخت. بدترین حالت کلاینت سه تایم‌اوت ۲۰ ثانیه‌ای است (خواندن، ساخت، خواندن دوباره)؛
    /// مهلت با فاصلهٔ ایمن پنج دقیقه انتخاب شده و از جمع تایم‌اوت‌های واقعی بیشتر است.
    /// </summary>
    public static readonly TimeSpan RequestLease = TimeSpan.FromMinutes(5);

    public async Task<CreatePhysicalShipmentResponse> Handle(
        CreatePhysicalShipmentCommand request, CancellationToken ct)
    {
        var owner = await deliveryStore.FindCartOwnerAsync(request.CartId, ct)
            ?? throw new NotFoundException(OrderErrors.CartNotFound);

        var active = ActiveItems(await deliveryStore.GetPurchasesAsync(request.CartId, ct));
        EnsureShippable(active);

        var client = ShipmentClientSelector.Resolve(shipmentClients, request.Provider);
        if (client is null || !client.IsConfigured)
        {
            throw new DomainException(OrderErrors.ShipmentProviderUnavailable);
        }

        var address = await deliveryStore.GetCartShippingAddressAsync(request.CartId, ct);
        if (address is null || !address.IsValid())
        {
            throw new DomainException(OrderErrors.ShipmentAddressRequired);
        }

        var originId = await deliveryStore.GetCartShippingOriginIdAsync(request.CartId, ct);
        if (string.IsNullOrWhiteSpace(originId))
        {
            throw new DomainException(OrderErrors.ShipmentOriginRequired);
        }

        var frozenCityId = await deliveryStore.GetCartShippingOriginCityIdAsync(request.CartId, ct);
        if (frozenCityId is int quotedCityId &&
            originCities?.GetCityId(originId) != quotedCityId)
        {
            throw new DomainException(OrderErrors.ShipmentOriginCityMismatch);
        }

        var collectionType = await ResolveCollectionTypeAsync(ct);
        var claimed = await ClaimAsync(request.CartId, request.BoxTypeId, owner, ct);

        var createResult = await client.CreateOrGetParcelAsync(
            BuildRequest(request.CartId, claimed.Parcel, address, originId, collectionType), ct);

        if (createResult.Outcome == ShipmentCreateOutcome.Uncertain)
        {
            throw new DomainException(
                string.IsNullOrWhiteSpace(createResult.ErrorMessage)
                    ? OrderErrors.ShipmentOutcomeUnknown
                    : createResult.ErrorMessage);
        }

        if (createResult.Outcome is ShipmentCreateOutcome.Failed or ShipmentCreateOutcome.NotConfigured)
        {
            await ReleaseReservationAsync(request.CartId, claimed.Lines, owner, ct);
            throw new DomainException(
                string.IsNullOrWhiteSpace(createResult.ErrorMessage)
                    ? OrderErrors.ShipmentCreateFailed
                    : createResult.ErrorMessage);
        }

        return await CommitResultAsync(request.CartId, claimed.Lines, client, createResult, owner, ct);
    }

    /// <summary>
    /// روش جمع‌آوری را پیش از رزرو می‌خواند. نبود خواننده فقط در آزمون است و پیش‌فرض مصوب را می‌دهد.
    /// قطع استرپی مرسوله نمی‌سازد.
    /// </summary>
    private async Task<string> ResolveCollectionTypeAsync(CancellationToken ct)
    {
        if (collectionTypes is null)
        {
            return ShippingCollectionTypes.Default;
        }

        var read = await collectionTypes.ReadAsync(ct);
        if (read.Status != ShippingCollectionStatus.Found ||
            !ShippingCollectionTypes.IsAllowed(read.CollectionType, out var normalized))
        {
            throw new DomainException(OrderErrors.ShipmentCollectionTypeUnavailable);
        }

        return normalized;
    }

    /// <summary>اعتبارسنجی دوباره، ساخت بدنهٔ مرسوله و رزرو همهٔ اقلام فعال؛ هیچ فراخوانی واسط اینجا نیست.</summary>
    private async Task<ShipmentClaim> ClaimAsync(
        Guid cartId, int? boxTypeId, PhysicalPurchaseOwner owner, CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, cartId, async () =>
        {
            var active = ActiveItems(await deliveryStore.GetPurchasesAsync(cartId, ct));
            EnsureShippable(active);
            var parcel = CartShipmentParcel.Build(cartId, active, boxTypeId);

            var now = DateTime.UtcNow;
            var started = new List<PhysicalProductPurchase>();
            foreach (var item in active)
            {
                if (!item.TryBeginShipmentRequest(now, RequestLease))
                {
                    foreach (var startedItem in started)
                    {
                        startedItem.AbandonShipmentRequest();
                    }

                    throw new ConflictDomainException(OrderErrors.ShipmentRequestInProgress);
                }

                started.Add(item);
            }

            foreach (var item in started)
            {
                repository.Update(item);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new ShipmentClaim(
                started.Select(item => new ClaimedLine(item.Id.Value, item.RemainingQuantity)).ToList(),
                parcel);
        }, ct);

    /// <summary>آزادکردن رزرو پس از رد صریح واسط؛ یک تراکنش تا پاک‌شدن رزرو حتماً commit شود.</summary>
    private async Task ReleaseReservationAsync(
        Guid cartId,
        IReadOnlyList<ClaimedLine> claimed,
        PhysicalPurchaseOwner owner,
        CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, cartId, async () =>
        {
            var purchases = await deliveryStore.GetPurchasesAsync(cartId, ct);
            foreach (var line in claimed)
            {
                var item = purchases.FirstOrDefault(purchase => purchase.Id.Value == line.PurchaseId);
                if (item is null)
                {
                    continue;
                }

                item.AbandonShipmentRequest();
                repository.Update(item);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);

    /// <summary>
    /// ثبت نتیجه روی همهٔ اقلامی که در رزرو بودند. قلمی که در این فاصله کامل لغو شده
    /// ارجاع مرسوله را می‌گیرد تا مرسوله گم نشود، ولی «ارسال‌شده» نمی‌شود.
    /// </summary>
    private async Task<CreatePhysicalShipmentResponse> CommitResultAsync(
        Guid cartId,
        IReadOnlyList<ClaimedLine> claimed,
        IShipmentProviderClient client,
        ShipmentCreateResult createResult,
        PhysicalPurchaseOwner owner,
        CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, cartId, async () =>
        {
            var parcelId = createResult.ProviderParcelId;
            if (string.IsNullOrWhiteSpace(parcelId))
            {
                throw new DomainException(OrderErrors.ShipmentOutcomeUnknown);
            }

            var purchases = await deliveryStore.GetPurchasesAsync(cartId, ct);
            var targets = new List<PhysicalProductPurchase>(claimed.Count);
            foreach (var line in claimed)
            {
                var item = purchases.FirstOrDefault(purchase => purchase.Id.Value == line.PurchaseId)
                    ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);
                targets.Add(item);
            }

            if (targets.Any(item =>
                    item.RemainingQuantity > 0 &&
                    item.DeliveryStatus != PhysicalDeliveryStatus.Preparing))
            {
                throw new ConflictDomainException(OrderErrors.ShipmentNotAllowed);
            }

            var now = DateTime.UtcNow;
            var pendingPayment = createResult.Outcome == ShipmentCreateOutcome.PendingPayment;
            var hasBarcode = PhysicalProductPurchase.IsUsablePostalTrackingCode(createResult.TrackingCode);
            var shippedAny = false;
            var cancelledAny = false;

            foreach (var item in targets)
            {
                if (item.DeliveryStatus != PhysicalDeliveryStatus.Preparing)
                {
                    throw new ConflictDomainException(OrderErrors.ShipmentNotAllowed);
                }

                var line = claimed.First(entry => entry.PurchaseId == item.Id.Value);
                var cancelled = item.RemainingQuantity == 0;
                if (cancelled)
                {
                    cancelledAny = true;
                }

                if (!cancelled && !pendingPayment && hasBarcode)
                {
                    item.MarkShipped(createResult.TrackingCode, client.Provider, parcelId);
                    item.RememberTrackingUrl(createResult.TrackingUrl);
                    shippedAny = true;
                }
                else
                {
                    item.RememberCarrierParcel(client.Provider, parcelId, line.Quantity);
                    item.RememberTrackingUrl(createResult.TrackingUrl);
                    item.AbandonShipmentRequest();
                    if (pendingPayment)
                    {
                        item.MarkCarrierPaymentPending(now);
                    }
                    else
                    {
                        item.ClearCarrierPaymentPending();
                    }
                }

                repository.Update(item);
            }

            await unitOfWork.SaveChangesAsync(ct);

            var representative = targets.FirstOrDefault(item => item.RemainingQuantity > 0) ?? targets[0];
            var warning = cancelledAny
                ? OrderErrors.ShipmentQuantityMismatch
                : pendingPayment
                    ? OrderErrors.ShipmentPaymentPendingHelp
                    : shippedAny
                        ? null
                        : OrderErrors.ShipmentTrackingCodePending;

            var outcome = shippedAny
                ? createResult.Outcome == ShipmentCreateOutcome.AlreadyExists
                    ? nameof(ShipmentCreateOutcome.AlreadyExists)
                    : nameof(ShipmentCreateOutcome.Created)
                : cancelledAny
                    ? nameof(ShipmentCreateOutcome.AlreadyExists)
                    : pendingPayment
                        ? nameof(ShipmentCreateOutcome.PendingPayment)
                        : "AwaitingTrackingCode";

            return new CreatePhysicalShipmentResponse(
                cartId,
                representative.DeliveryStatus.ToString(),
                representative.ShippedAtUtc,
                representative.TrackingCode,
                representative.TrackingProvider,
                representative.ProviderParcelId,
                outcome,
                warning,
                representative.CarrierPaymentPendingSinceUtc);
        }, ct);

    private static List<PhysicalProductPurchase> ActiveItems(
        IReadOnlyList<PhysicalProductPurchase> purchases) =>
        purchases
            .Where(purchase => purchase.RemainingQuantity > 0)
            .OrderBy(purchase => purchase.Id.Value)
            .ToList();

    private static void EnsureShippable(IReadOnlyList<PhysicalProductPurchase> active)
    {
        if (active.Count == 0)
        {
            throw new DomainException(OrderErrors.ShipmentNoActiveItems);
        }

        if (active.Any(item =>
                item.DeliveryStatus is PhysicalDeliveryStatus.Shipped or PhysicalDeliveryStatus.Delivered))
        {
            throw new DomainException(OrderErrors.ShipmentAlreadyDispatched);
        }

        if (active.Any(item => item.DeliveryStatus != PhysicalDeliveryStatus.Preparing))
        {
            throw new DomainException(OrderErrors.ShipmentNotAllowed);
        }

        if (active.Any(item => !item.HasParcelFacts))
        {
            throw new DomainException(OrderErrors.ShipmentParcelRequired);
        }
    }

    private static ShipmentCreateRequest BuildRequest(
        Guid cartId,
        ShipmentParcelDetails parcel,
        ShippingAddress address,
        string originId,
        string? collectionType) =>
        new(
            new ShipmentParty(
                address.RecipientName,
                address.RecipientPhone,
                Telephone: address.RecipientPhone,
                NationalCode: null,
                CompanyName: null,
                address.FullAddress,
                address.City,
                address.Province,
                address.PostalCode,
                address.CityDocumentId),
            parcel,
            Remark: $"ByeMoney cart {cartId:N}",
            OriginId: originId.Trim(),
            CollectionType: collectionType);

    private sealed record ClaimedLine(Guid PurchaseId, int Quantity);

    private sealed record ShipmentClaim(IReadOnlyList<ClaimedLine> Lines, ShipmentParcelDetails Parcel);
}
