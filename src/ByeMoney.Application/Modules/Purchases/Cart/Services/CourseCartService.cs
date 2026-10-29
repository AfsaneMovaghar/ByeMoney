using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Cart.Services;

public sealed class CourseCartService(
    ICourseCartStore store,
    ICartPricingClient pricing,
    IUserRepository users,
    IWalletRepository wallets,
    ISystemSettingRepository settings,
    ICoursePurchaseRepository purchases,
    ITopUpRequestRepository topUps)
{
    public Task<Result<CartResponse>> PrepareAsync(
        Guid buyerId,
        Guid id,
        string[] ids,
        string? code,
        CancellationToken ct) =>
        PrepareAsync(buyerId, id, ids.Select(x => new CartItemInput(x, ProductItemType.Course, 1)).ToArray(), code, ct);

    public async Task<Result<CartResponse>> PrepareAsync(
        Guid buyerId,
        Guid id,
        CartItemInput[] items,
        string? code,
        CancellationToken ct)
    {
        var user = await users.GetByIdAsync(new UserId(buyerId), ct);
        if (user is null || !user.IsActive)
        {
            return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        var courseExternalIds = items
            .Where(x => x.Type != ProductItemType.Product)
            .Select(x => x.ExternalId)
            .Distinct()
            .ToArray();

        if (courseExternalIds.Length > 0 && await purchases.HasPurchasedAnyAsync(new UserId(buyerId), courseExternalIds, ct: ct))
        {
            return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        var created = await store.CreateAsync(CourseCart.Create(id, buyerId, user.ExternalUserId, items, code), ct);
        if (created.IsFailure)
        {
            return Copy<CartResponse, CourseCart>(created);
        }

        var cart = created.Value;
        if (cart.Status == CourseCartStatus.AwaitingTopUp && cart.TopUpRequestId is Guid previousTopUpId)
        {
            var previousTopUp = await topUps.GetByIdAsync(new TopUpRequestId(previousTopUpId), ct);
            if (previousTopUp?.Status is TopUpStatus.Pending or TopUpStatus.UnderReview or TopUpStatus.Rejected or TopUpStatus.Unresolved or TopUpStatus.ManuallyRefunded)
            {
                await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                cart = (await store.FindAsync(id, buyerId, ct))!;
            }
        }

        if (cart.Status is CourseCartStatus.Preparing or CourseCartStatus.Ready && cart.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await store.FailAsync(id, buyerId, true, cart.ReservationId, ct);
            cart = (await store.FindAsync(id, buyerId, ct))!;
        }

        if (cart.Status is CourseCartStatus.Failed or CourseCartStatus.Expired)
        {
            await SynchronizeAsync(id, buyerId, ct);
            var synchronized = await store.FindAsync(id, buyerId, ct);
            if (synchronized?.ReservationId == cart.ReservationId && !synchronized.Synchronized)
            {
                return Result<CartResponse>.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable);
            }

            var reopened = await store.ReopenAsync(id, buyerId, cart.ReservationId, ct);
            if (reopened.IsFailure)
            {
                return Copy<CartResponse, CourseCart>(reopened);
            }

            cart = reopened.Value;
        }

        if (cart.Status == CourseCartStatus.Preparing && cart.ExpiresAtUtc > DateTime.UtcNow)
        {
            CartQuoteRequest quoteRequest;
            if (items.Any(x => x.Type == ProductItemType.Product || x.Quantity > 1))
            {
                var requestItems = items.Select(x => new CartQuoteItemRequest(
                    x.ExternalId,
                    x.Type switch
                    {
                        ProductItemType.CourseChapter => "course_chapter",
                        ProductItemType.Product => "product",
                        _ => "course"
                    },
                    x.Quantity)).ToArray();
                quoteRequest = new CartQuoteRequest(cart.ReservationId, user.ExternalUserId, null, cart.CouponCode, requestItems);
            }
            else
            {
                quoteRequest = new CartQuoteRequest(cart.ReservationId, user.ExternalUserId, cart.ExternalCourseIds, cart.CouponCode);
            }

            var quote = await pricing.PrepareAsync(quoteRequest, ct);
            if (quote.IsFailure)
            {
                if (quote.ErrorCode != CartErrorCodes.Unavailable)
                {
                    await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                    await SynchronizeAsync(id, buyerId, ct);
                }

                return Copy<CartResponse, CartQuote>(quote);
            }

            if (quote.Value.CartId != cart.ReservationId ||
                quote.Value.BuyerExternalUserId != user.ExternalUserId ||
                quote.Value.CouponCode != cart.CouponCode)
            {
                return Result<CartResponse>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid);
            }

            var quoteProductIds = quote.Value.Items
                .Where(x => x.Type != ProductItemType.Product)
                .Select(x => x.ExternalCourseId)
                .ToArray();
            var quoteParentIds = quote.Value.Items
                .Where(x => x.Type == ProductItemType.CourseChapter && !string.IsNullOrEmpty(x.ParentExternalId))
                .Select(x => x.ParentExternalId!)
                .Distinct()
                .ToArray();

            if (await purchases.HasPurchasedAnyAsync(new UserId(buyerId), quoteProductIds, quoteParentIds, ct))
            {
                await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                await SynchronizeAsync(id, buyerId, ct);
                return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
            }

            try
            {
                cart = await store.SetQuoteAsync(id, buyerId, quote.Value.Items, quote.Value.ShippingAmountInNoor, quote.Value.ShippingRuleLabel, cart.ReservationId, ct);
            }
            catch (ArgumentException)
            {
                await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                await SynchronizeAsync(id, buyerId, ct);
                return Result<CartResponse>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid);
            }
        }

        if (cart.Status == CourseCartStatus.Ready)
        {
            if (cart.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
            }

            var available = await pricing.TransitionAsync(new(cart.ReservationId, cart.BuyerExternalUserId), "check", ct);
            if (available.IsFailure)
            {
                return Copy<CartResponse>(available);
            }
        }

        return Result<CartResponse>.Success(await ResponseAsync(cart, ct));
    }

    public async Task<Result<CartResponse>> CancelAsync(Guid buyerId, Guid id, CancellationToken ct)
    {
        var result = await store.CancelAsync(id, buyerId, ct);
        if (result.IsFailure)
        {
            return Copy<CartResponse, CourseCart>(result);
        }

        await SynchronizeAsync(id, buyerId, ct);

        // پیش از آماده‌سازی سبد جایگزین، آزادشدن رزرو کوپن باید تأیید شده باشد.
        var cart = await store.FindAsync(id, buyerId, ct);
        if (cart is { Status: CourseCartStatus.Failed or CourseCartStatus.Expired, Synchronized: false })
        {
            return Result<CartResponse>.Failure(CartErrors.Unavailable, CartErrorCodes.Unavailable);
        }

        return Result<CartResponse>.Success(await ResponseAsync(cart!, ct));
    }

    public async Task<Result<CartResponse>> GetAsync(Guid buyerId, Guid id, CancellationToken ct)
    {
        var cart = await store.FindAsync(id, buyerId, ct);
        if (cart is null)
        {
            return Result<CartResponse>.NotFound(CartErrors.NotFound, CartErrorCodes.NotFound);
        }

        return Result<CartResponse>.Success(await ResponseAsync(cart, ct));
    }

    public async Task<Result<CartResponse>> PurchaseAsync(Guid buyerId, Guid id, CancellationToken ct, Guid? reservationId = null)
    {
        var cart = await store.FindAsync(id, buyerId, ct);
        if (cart is null)
        {
            return Result<CartResponse>.NotFound(CartErrors.NotFound, CartErrorCodes.NotFound);
        }

        if (reservationId.HasValue && cart.ReservationId != reservationId.Value)
        {
            return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        if (cart.Status == CourseCartStatus.Paid)
        {
            await SynchronizeAsync(id, buyerId, ct);
            return await GetAsync(buyerId, id, ct);
        }

        if (cart.Status is not (CourseCartStatus.Ready or CourseCartStatus.AwaitingTopUp))
        {
            return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        if (cart.Status == CourseCartStatus.Ready && cart.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await store.FailAsync(id, buyerId, true, cart.ReservationId, ct);
            await SynchronizeAsync(id, buyerId, ct);
            return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        if (cart.TopUpRequestId is Guid topUpId)
        {
            var topUp = await topUps.GetByIdAsync(new TopUpRequestId(topUpId), ct);
            if (topUp?.Status != TopUpStatus.Confirmed)
            {
                return Result<CartResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
            }
        }

        var check = await pricing.TransitionAsync(new(cart.ReservationId, cart.BuyerExternalUserId), "check", ct);
        if (check.IsFailure)
        {
            var latest = await store.FindAsync(id, buyerId, ct);
            if (latest?.Status == CourseCartStatus.Paid)
            {
                return await GetAsync(buyerId, id, ct);
            }

            if (check.ErrorCode != CartErrorCodes.Unavailable)
            {
                await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                await SynchronizeAsync(id, buyerId, ct);
            }

            return Copy<CartResponse>(check);
        }

        var referenceRate = await PurchaseReferenceRateReader.ReadAsync(settings, ct);
        var result = await store.PayAsync(id, buyerId, cart.ReservationId, ct, referenceRate);
        if (result.IsFailure)
        {
            if (result.ErrorCode == CartErrorCodes.Conflict || cart.Status == CourseCartStatus.AwaitingTopUp)
            {
                await store.FailAsync(id, buyerId, false, cart.ReservationId, ct);
                await SynchronizeAsync(id, buyerId, ct);
            }

            return Copy<CartResponse, CourseCart>(result);
        }

        await SynchronizeAsync(id, buyerId, ct);
        return await GetAsync(buyerId, id, ct);
    }

    public async Task<Result<CreateTopUpRequestResponse>> TopUpAsync(Guid buyerId, Guid id, CancellationToken ct)
    {
        var cart = await store.FindAsync(id, buyerId, ct);
        if (cart is null)
        {
            return Result<CreateTopUpRequestResponse>.NotFound(CartErrors.NotFound, CartErrorCodes.NotFound);
        }

        return await store.CreateTopUpAsync(id, buyerId, ct);
    }

    public async Task SynchronizeAsync(Guid id, Guid buyerId, CancellationToken ct)
    {
        var cart = await store.FindAsync(id, buyerId, ct);
        if (cart is null || cart.Synchronized || cart.Status is not (CourseCartStatus.Paid or CourseCartStatus.Failed or CourseCartStatus.Expired))
        {
            return;
        }

        var paid = cart.Status == CourseCartStatus.Paid;
        var purchaseIds = paid
            ? cart.Items.Select(x => CartPurchaseIds.For(id, x.ExternalCourseId).Value).ToArray()
            : null;

        var result = await pricing.TransitionAsync(
            new(cart.ReservationId, cart.BuyerExternalUserId, purchaseIds),
            paid ? "commit" : "release",
            ct);

        await store.RecordSynchronizationAsync(id, buyerId, result.IsSuccess, result.ErrorCode, cart.ReservationId, ct);
    }

    public async Task ProcessAsync(CourseCart cart, CancellationToken ct)
    {
        if (cart.Status is CourseCartStatus.Preparing or CourseCartStatus.Ready && cart.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await store.FailAsync(cart.Id, cart.BuyerId, true, cart.ReservationId, ct);
        }

        if (cart.Status == CourseCartStatus.AwaitingTopUp && cart.TopUpRequestId is Guid topUpId)
        {
            var topUp = await topUps.GetByIdAsync(new TopUpRequestId(topUpId), ct);
            if (topUp?.Status == TopUpStatus.Confirmed)
            {
                await PurchaseAsync(cart.BuyerId, cart.Id, ct, cart.ReservationId);
            }
            else if (topUp?.Status is TopUpStatus.Rejected or TopUpStatus.ManuallyRefunded or TopUpStatus.Unresolved or TopUpStatus.UnderReview)
            {
                await store.FailAsync(cart.Id, cart.BuyerId, false, cart.ReservationId, ct);
            }
        }

        await SynchronizeAsync(cart.Id, cart.BuyerId, ct);
    }

    private async Task<CartResponse> ResponseAsync(CourseCart cart, CancellationToken ct)
    {
        var wallet = await wallets.GetByUserIdAsync(new UserId(cart.BuyerId), ct);
        var balance = wallet?.TotalBalance ?? 0m;
        var shortfall = cart.Status == CourseCartStatus.Ready ? decimal.Ceiling(Math.Max(0, cart.Total - balance)) : 0m;
        var rate = shortfall > 0 ? await settings.GetRialToNoorConversionRateAsync(ct) : 0m;

        if (shortfall > 0 && (rate <= 0 || decimal.Truncate(rate) != rate))
        {
            throw new InvalidOperationException(CartErrors.Invalid);
        }

        var courses = new List<CartCourseResponse>();
        foreach (var item in cart.Items)
        {
            var purchase = await purchases.GetByIdAsync(CartPurchaseIds.For(cart.Id, item.ExternalCourseId), ct);
            var status = purchase?.Status == CoursePurchaseStatus.NotificationSent
                ? "Completed"
                : cart.Status == CourseCartStatus.Paid
                    ? (item.ItemType == ProductItemType.Product ? "Completed" : "DeliveryPending")
                    : cart.Status is CourseCartStatus.Failed or CourseCartStatus.Expired
                        ? "PurchaseIncomplete"
                        : "NotPurchased";

            courses.Add(new(
                item.ExternalCourseId,
                item.Title,
                item.PriceInNoor,
                status,
                item.ItemType.ToString(),
                item.ParentExternalId,
                item.Quantity,
                item.LineTotalInNoor));
        }

        var topUp = cart.TopUpRequestId is Guid topUpId
            ? await topUps.GetByIdAsync(new TopUpRequestId(topUpId), ct)
            : null;

        return new CartResponse(
            cart.Id,
            cart.Status.ToString(),
            cart.CouponCode,
            cart.OriginalTotal,
            cart.ItemDiscountInNoor,
            cart.CouponDiscountInNoor,
            cart.Total,
            balance,
            shortfall,
            shortfall * rate,
            cart.ExpiresAtUtc,
            cart.TopUpRequestId,
            topUp?.Status.ToString(),
            courses,
            cart.ShippingAmountInNoor,
            cart.ShippingRuleLabel,
            topUp?.IsUnderReview ?? false,
            topUp?.ReviewCaseId,
            topUp?.ReviewReasonCode,
            topUp?.GatewayResultKind);
    }

    private static Result<T> Copy<T, U>(Result<U> result) => Copy<T>((Result)result);

    private static Result<T> Copy<T>(Result result) => result.Status switch
    {
        ResultStatus.NotFound => Result<T>.NotFound(result.ErrorMessage, result.ErrorCode),
        ResultStatus.Conflict => Result<T>.Conflict(result.ErrorMessage!, result.ErrorCode),
        _ => Result<T>.Failure(result.ErrorMessage!, result.ErrorCode)
    };
}

