using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
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
/// ساخت مرسوله از طریق پورت عمومی <see cref="IShipmentProviderClient"/>؛
/// جزئیات پستکس/واسط دیگر در Infrastructure می‌ماند.
/// فراخوانی واسط بیرون از تراکنش و قفل انجام می‌شود: اول یک رزرو کوتاه روی خرید ثبت می‌شود،
/// بعد HTTP (بدون قفل) و در پایان نتیجه در یک تراکنش کوتاه ذخیره می‌شود. رزرو فقط با رد صریح
/// پیش از ارسال یا پاسخ قطعی ناموفق آزاد می‌شود؛ در نتیجهٔ نامعلوم می‌ماند تا خودش منقضی شود.
/// </summary>
public sealed class CreatePhysicalShipmentCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IEnumerable<IShipmentProviderClient> shipmentClients,
    IUnitOfWork unitOfWork) :
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
        var owner = await deliveryStore.FindOwnerAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        var client = ShipmentClientSelector.Resolve(shipmentClients, request.Provider);
        if (client is null || !client.IsConfigured)
        {
            throw new DomainException(OrderErrors.ShipmentProviderUnavailable);
        }

        var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        if (IsTrackedShipment(purchase, client.Provider))
        {
            return AlreadyShipped(purchase);
        }

        if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing || purchase.RemainingQuantity == 0)
        {
            throw new DomainException(OrderErrors.ShipmentNotAllowed);
        }

        // عوامل مرسوله پیش از هر فراخوانی واسط بررسی می‌شوند؛ خرید قدیمی بدون parcel عدد جایگزین نمی‌گیرد.
        var parcelFacts = RequireParcelFacts(purchase);

        var address = await deliveryStore.GetCartShippingAddressAsync(owner.CartId, ct);
        if (address is null || !address.IsValid())
        {
            throw new DomainException(OrderErrors.ShipmentAddressRequired);
        }

        var originId = await deliveryStore.GetCartShippingOriginIdAsync(owner.CartId, ct);
        if (string.IsNullOrWhiteSpace(originId))
        {
            throw new DomainException(OrderErrors.ShipmentOriginRequired);
        }

        // تعدادی که در همین درخواست به واسط می‌رود؛ روی مرسوله ثبت می‌شود تا نگهبان ارتقای خودکار
        // آن را با تعداد باقی‌مانده بسنجد.
        var requestedQuantity = purchase.RemainingQuantity;

        var early = await ClaimAsync(request.PurchaseId, client, owner, ct);
        if (early is not null)
        {
            return early;
        }

        var createResult = await client.CreateOrGetParcelAsync(
            BuildRequest(purchase, requestedQuantity, address, originId, parcelFacts, request.CollectionType), ct);

        if (createResult.Outcome == ShipmentCreateOutcome.Uncertain)
        {
            // درخواست ممکن است رسیده باشد؛ رزرو نمی‌شود تا تلاش بعدی اول بخواند.
            throw new DomainException(
                string.IsNullOrWhiteSpace(createResult.ErrorMessage)
                    ? OrderErrors.ShipmentOutcomeUnknown
                    : createResult.ErrorMessage);
        }

        if (createResult.Outcome is ShipmentCreateOutcome.Failed or ShipmentCreateOutcome.NotConfigured)
        {
            // رد صریح: مرسوله‌ای ساخته نشد، رزرو آزاد می‌شود.
            await ReleaseReservationAsync(request.PurchaseId, owner, ct);
            throw new DomainException(
                string.IsNullOrWhiteSpace(createResult.ErrorMessage)
                    ? OrderErrors.ShipmentCreateFailed
                    : createResult.ErrorMessage);
        }

        return await CommitResultAsync(request.PurchaseId, client, createResult, requestedQuantity, owner, ct);
    }

    /// <summary>اعتبارسنجی تکراری و رزرو کوتاه؛ هیچ فراخوانی واسط اینجا نیست.</summary>
    private async Task<CreatePhysicalShipmentResponse?> ClaimAsync(
        Guid purchaseId, IShipmentProviderClient client, PhysicalPurchaseOwner owner, CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await LoadAsync(purchaseId, ct);

            if (IsTrackedShipment(locked, client.Provider))
            {
                return AlreadyShipped(locked);
            }

            if (locked.DeliveryStatus != PhysicalDeliveryStatus.Preparing || locked.RemainingQuantity == 0)
            {
                throw new DomainException(OrderErrors.ShipmentNotAllowed);
            }

            if (!locked.TryBeginShipmentRequest(DateTime.UtcNow, RequestLease))
            {
                throw new ConflictDomainException(OrderErrors.ShipmentRequestInProgress);
            }

            repository.Update(locked);
            await unitOfWork.SaveChangesAsync(ct);
            return (CreatePhysicalShipmentResponse?)null;
        }, ct);

    /// <summary>آزادکردن رزرو پس از رد صریح واسط؛ تراکنش کوتاه و مستقل تا پاک‌شدن رزرو حتماً commit شود.</summary>
    private async Task ReleaseReservationAsync(
        Guid purchaseId, PhysicalPurchaseOwner owner, CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await LoadAsync(purchaseId, ct);
            locked.AbandonShipmentRequest();
            repository.Update(locked);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);

    /// <summary>
    /// ثبت نتیجهٔ واسط. قلم ممکن است در همین فاصله لغو شده باشد؛ در آن حالت ارجاع مرسوله
    /// باز هم ذخیره می‌شود تا مرسولهٔ واسط گم نشود، ولی وضعیت تحویل عوض نمی‌شود.
    /// </summary>
    private async Task<CreatePhysicalShipmentResponse> CommitResultAsync(
        Guid purchaseId,
        IShipmentProviderClient client,
        ShipmentCreateResult createResult,
        int requestedQuantity,
        PhysicalPurchaseOwner owner,
        CancellationToken ct) =>
        await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await LoadAsync(purchaseId, ct);
            var parcelId = createResult.ProviderParcelId;

            if (string.IsNullOrWhiteSpace(parcelId))
            {
                // بدون شناسه نمی‌توان «در انتظار پرداخت» را ثبت کرد؛ رزرو عمداً آزاد نمی‌شود.
                throw new DomainException(OrderErrors.ShipmentOutcomeUnknown);
            }

            if (locked.DeliveryStatus != PhysicalDeliveryStatus.Preparing)
            {
                throw new ConflictDomainException(OrderErrors.ShipmentNotAllowed);
            }

            var cancelledMeanwhile = locked.RemainingQuantity == 0;
            var pendingPayment = createResult.Outcome == ShipmentCreateOutcome.PendingPayment;

            if (!cancelledMeanwhile && !pendingPayment &&
                PhysicalProductPurchase.IsUsablePostalTrackingCode(createResult.TrackingCode))
            {
                locked.MarkShipped(createResult.TrackingCode, client.Provider, parcelId);
                locked.RememberTrackingUrl(createResult.TrackingUrl);
                repository.Update(locked);
                await unitOfWork.SaveChangesAsync(ct);
                return Response(locked,
                    createResult.Outcome == ShipmentCreateOutcome.AlreadyExists
                        ? nameof(ShipmentCreateOutcome.AlreadyExists)
                        : nameof(ShipmentCreateOutcome.Created));
            }

            locked.RememberCarrierParcel(client.Provider, parcelId, requestedQuantity);
            locked.RememberTrackingUrl(createResult.TrackingUrl);
            // تلاش با نتیجهٔ معلوم تمام شد؛ رزرو آزاد می‌شود تا تلاش بعدی فقط با خواندن پاسخ بگیرد.
            locked.AbandonShipmentRequest();
            if (pendingPayment)
            {
                locked.MarkCarrierPaymentPending(DateTime.UtcNow);
            }
            else
            {
                locked.ClearCarrierPaymentPending();
            }

            repository.Update(locked);
            await unitOfWork.SaveChangesAsync(ct);

            if (cancelledMeanwhile)
            {
                return Response(locked, nameof(ShipmentCreateOutcome.AlreadyExists),
                    OrderErrors.ShipmentQuantityMismatch);
            }

            return pendingPayment
                ? Response(locked, nameof(ShipmentCreateOutcome.PendingPayment), OrderErrors.ShipmentPaymentPendingHelp)
                : Response(locked, "AwaitingTrackingCode", OrderErrors.ShipmentTrackingCodePending);
        }, ct);

    private async Task<PhysicalProductPurchase> LoadAsync(Guid purchaseId, CancellationToken ct) =>
        await repository.GetByIdAsync(new PhysicalProductPurchaseId(purchaseId), ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

    private static CreatePhysicalShipmentResponse Response(
        PhysicalProductPurchase purchase, string outcome, string? warning = null) =>
        CreatePhysicalShipmentResponse.From(
            new PhysicalDeliveryStatusResponse(
                purchase.Id.Value, purchase.DeliveryStatus.ToString(),
                purchase.ShippedAtUtc, purchase.DeliveredAtUtc,
                purchase.TrackingCode, purchase.TrackingProvider,
                purchase.CarrierPaymentPendingSinceUtc),
            purchase.ProviderParcelId, outcome, warning);

    private static bool IsTrackedShipment(PhysicalProductPurchase purchase, string provider) =>
        purchase.DeliveryStatus == PhysicalDeliveryStatus.Shipped &&
        !string.IsNullOrWhiteSpace(purchase.ProviderParcelId) &&
        string.Equals(purchase.TrackingProvider, provider, StringComparison.OrdinalIgnoreCase);

    private static CreatePhysicalShipmentResponse AlreadyShipped(PhysicalProductPurchase purchase) =>
        new(purchase.Id.Value, purchase.DeliveryStatus.ToString(), purchase.ShippedAtUtc,
            purchase.TrackingCode, purchase.TrackingProvider, purchase.ProviderParcelId,
            nameof(ShipmentCreateOutcome.AlreadyExists), CarrierPaymentPendingSinceUtc: null);

    private static ShipmentCreateRequest BuildRequest(
        PhysicalProductPurchase purchase,
        int quantity,
        ShippingAddress address,
        string originId,
        ParcelFacts parcelFacts,
        string? collectionType)
    {
        // کلید یکتا در سطح بای‌مانی؛ پستکس خودش تکرار را مسدود نمی‌کند و کلید عوض نمی‌شود.
        var idempotencyKey = $"BM-{purchase.Id.Value:N}";

        return new ShipmentCreateRequest(
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
            new ShipmentParcelDetails(
                idempotencyKey,
                purchase.ProductTitle,
                quantity,
                parcelFacts),
            Remark: $"ByeMoney purchase {purchase.Id.Value:N}",
            OriginId: originId.Trim(),
            CollectionType: collectionType);
    }

    private static ParcelFacts RequireParcelFacts(PhysicalProductPurchase purchase) =>
        purchase.HasParcelFacts && purchase.Parcel is { } parcelFacts
            ? parcelFacts
            : throw new DomainException(OrderErrors.ShipmentParcelRequired);
}
