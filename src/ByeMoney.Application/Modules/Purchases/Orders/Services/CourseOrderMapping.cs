using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.EventHandlers;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class CourseOrderMapping
{
    public static IReadOnlyList<CourseOrderReadRow> Build(CourseOrderSnapshot snapshot)
    {
        var rows = new List<CourseOrderReadRow>();
        var purchasesByCart = snapshot.Purchases.Where(purchase => purchase.CartId.HasValue)
            .ToLookup(purchase => purchase.CartId!.Value);
        var topUpsById = snapshot.TopUps.ToDictionary(topUp => topUp.Id.Value);
        var attachedTopUps = snapshot.Carts.SelectMany(TopUpIds).ToHashSet();
        var groupedPurchases = new HashSet<Guid>();

        foreach (var cart in snapshot.Carts)
        {
            var purchases = purchasesByCart[cart.Id].ToArray();
            var ids = TopUpIds(cart).ToArray();
            var topUps = ids.Where(topUpsById.ContainsKey).Select(id => topUpsById[id]).ToArray();
            var invalidLink = topUps.Length != ids.Length ||
                topUps.Any(topUp => topUp.UserId.Value != cart.BuyerId) ||
                purchases.Any(purchase => purchase.BuyerId.Value != cart.BuyerId);
            var ownedTopUps = topUps.Where(topUp => topUp.UserId.Value == cart.BuyerId).ToArray();
            var ownedPurchases = purchases.Where(purchase => purchase.BuyerId.Value == cart.BuyerId).ToArray();
            groupedPurchases.UnionWith(ownedPurchases.Select(purchase => purchase.Id.Value));
            rows.Add(new CourseOrderReadRow(cart.BuyerId,
                MapCart(cart, ownedPurchases, ownedTopUps, invalidLink)));
        }

        var purchasesById = snapshot.Purchases.ToDictionary(purchase => purchase.Id);
        foreach (var topUp in snapshot.TopUps.Where(topUp => !attachedTopUps.Contains(topUp.Id.Value)))
        {
            var pending = topUp.PendingItems.Where(item => item.ItemType == PendingItemType.Course)
                .DistinctBy(item => item.ExternalId).ToArray();
            if (pending.Length == 0)
            {
                continue;
            }

            var purchases = pending.Select(item => TopUpPurchaseId.For(topUp.Id, item.ExternalId))
                .Where(purchasesById.ContainsKey).Select(id => purchasesById[id])
                .Where(purchase => purchase.CartId is null && purchase.BuyerId == topUp.UserId).ToArray();
            groupedPurchases.UnionWith(purchases.Select(purchase => purchase.Id.Value));
            rows.Add(new CourseOrderReadRow(topUp.UserId.Value, MapTopUp(topUp, pending, purchases)));
        }

        foreach (var purchase in snapshot.Purchases.Where(purchase => !groupedPurchases.Contains(purchase.Id.Value)))
        {
            var status = purchase.CartId.HasValue
                ? CourseOrderFinancialStatus.RequiresReview
                : IsPaid(purchase) ? CourseOrderFinancialStatus.Paid : CourseOrderFinancialStatus.Processing;
            var item = MapItem(purchase.Snapshot.ExternalProductId, null, null, purchase, status);
            rows.Add(new CourseOrderReadRow(purchase.BuyerId.Value, new CourseOrderDetails(
                CourseOrderId.For(CourseOrderSourceType.Purchase, purchase.Id.Value),
                CourseOrderSourceType.Purchase.ToString(), purchase.CartId, purchase.CreatedAtUtc,
                PurchaseUpdatedAt(purchase), status.ToString(), Delivery([item]).ToString(),
                purchase.Snapshot.PriceInNoorAtPurchaseTime, 1, null, null, null, null, [item], [])));
        }

        return rows;
    }

    private static CourseOrderDetails MapCart(
        CourseCart cart, CoursePurchase[] purchases, TopUpRequest[] topUps, bool invalidLink)
    {
        var expectedIds = cart.ExternalCourseIds.Distinct(StringComparer.Ordinal).ToArray();
        var complete = CompletePurchases(expectedIds, purchases);
        var hasPurchases = purchases.Any(IsPaid);
        var consistent = !invalidLink &&
            purchases.All(purchase => expectedIds.Contains(purchase.Snapshot.ExternalProductId, StringComparer.Ordinal)) &&
            purchases.GroupBy(purchase => purchase.Snapshot.ExternalProductId).All(group => group.Count() == 1) &&
            purchases.Where(IsPaid).All(purchase => cart.PurchaseAllocations.Any(allocation =>
                allocation.ExternalCourseId == purchase.Snapshot.ExternalProductId &&
                allocation.AmountInNoor == purchase.Snapshot.PriceInNoorAtPurchaseTime)) &&
            (cart.Status != CourseCartStatus.AwaitingTopUp || cart.TopUpRequestId.HasValue);
        var current = topUps.SingleOrDefault(topUp => topUp.Id.Value == cart.TopUpRequestId);
        var status = CartFinancialStatus(cart, complete, hasPurchases, consistent, current);
        var ids = expectedIds.Concat(purchases.Select(purchase => purchase.Snapshot.ExternalProductId))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var items = ids.Select(id =>
        {
            var quote = cart.Items.SingleOrDefault(item => item.ExternalCourseId == id);
            var allocation = cart.PurchaseAllocations.SingleOrDefault(item => item.ExternalCourseId == id);
            var purchase = purchases.FirstOrDefault(item => item.Snapshot.ExternalProductId == id);
            return MapItem(id, quote?.Title, quote?.PriceInNoor, purchase, status, allocation?.AmountInNoor,
                quote?.ItemType, quote?.ParentExternalId);
        }).ToArray();
        var hasQuote = cart.Items.Count > 0 && expectedIds.All(
            id => cart.Items.Any(item => item.ExternalCourseId == id));
        var summary = hasQuote ? new PurchaseCartSummary(
            cart.Items.Sum(item => item.PriceInNoor), cart.ItemDiscountInNoor,
            cart.CouponDiscountInNoor, cart.Total) : null;
        return new CourseOrderDetails(
            CourseOrderId.For(CourseOrderSourceType.Cart, cart.Id), CourseOrderSourceType.Cart.ToString(),
            cart.Id, cart.CreatedAtUtc, UpdatedAt(cart.UpdatedAtUtc, purchases, topUps),
            status.ToString(), Delivery(items).ToString(), summary?.FinalAmountInNoor, expectedIds.Length,
            cart.Status.ToString(), cart.ExpiresAtUtc, cart.TopUpRequestId, summary, items, MapTopUps(topUps));
    }

    private static CourseOrderFinancialStatus CartFinancialStatus(
        CourseCart cart, bool complete, bool hasPurchases, bool consistent, TopUpRequest? current)
    {
        if (consistent && complete && cart.Status == CourseCartStatus.Paid)
        {
            return CourseOrderFinancialStatus.Paid;
        }
        if (!consistent || hasPurchases || cart.Status == CourseCartStatus.Paid || NeedsReview(current))
        {
            return CourseOrderFinancialStatus.RequiresReview;
        }

        return cart.Status switch
        {
            CourseCartStatus.Failed => CourseOrderFinancialStatus.Failed,
            CourseCartStatus.Expired => CourseOrderFinancialStatus.Expired,
            CourseCartStatus.Preparing => CourseOrderFinancialStatus.Preparing,
            CourseCartStatus.AwaitingTopUp when current?.Status == TopUpStatus.Confirmed => CourseOrderFinancialStatus.Processing,
            CourseCartStatus.AwaitingTopUp when current?.Status is TopUpStatus.Rejected or TopUpStatus.ManuallyRefunded => CourseOrderFinancialStatus.Failed,
            _ => CourseOrderFinancialStatus.AwaitingPayment
        };
    }

    private static CourseOrderDetails MapTopUp(
        TopUpRequest topUp, PendingItemSnapshot[] pending, CoursePurchase[] purchases)
    {
        var expectedIds = pending.Select(item => item.ExternalId).ToArray();
        var consistent = purchases.All(purchase => pending.Any(item =>
            item.ExternalId == purchase.Snapshot.ExternalProductId &&
            item.PriceNoorSnapshot == purchase.Snapshot.PriceInNoorAtPurchaseTime));
        var complete = consistent && CompletePurchases(expectedIds, purchases);
        var status = complete && topUp.Status == TopUpStatus.Confirmed
            ? CourseOrderFinancialStatus.Paid
            : !consistent || NeedsReview(topUp) || purchases.Any(IsPaid)
                ? CourseOrderFinancialStatus.RequiresReview
                : topUp.Status switch
                {
                    TopUpStatus.Pending => CourseOrderFinancialStatus.AwaitingPayment,
                    TopUpStatus.Rejected or TopUpStatus.ManuallyRefunded => CourseOrderFinancialStatus.Failed,
                    _ => CourseOrderFinancialStatus.RequiresReview
                };
        var items = pending.Select(item => MapItem(item.ExternalId, null, item.PriceNoorSnapshot,
            purchases.FirstOrDefault(purchase => purchase.Snapshot.ExternalProductId == item.ExternalId),
            status, item.PriceNoorSnapshot)).ToArray();
        return new CourseOrderDetails(
            CourseOrderId.For(CourseOrderSourceType.TopUpCheckout, topUp.Id.Value),
            CourseOrderSourceType.TopUpCheckout.ToString(), null, topUp.CreatedAtUtc,
            UpdatedAt(topUp.UpdatedAt ?? topUp.CreatedAtUtc, purchases, [topUp]),
            status.ToString(), Delivery(items).ToString(), items.Sum(item => item.FinalAmountInNoor),
            items.Length, null, null, topUp.Id.Value, null, items, MapTopUps([topUp]));
    }

    private static CourseOrderItem MapItem(
        string externalId, string? title, decimal? basePrice, CoursePurchase? purchase,
        CourseOrderFinancialStatus orderStatus, decimal? allocation = null,
        ProductItemType? itemType = null, string? parentExternalId = null)
    {
        var paid = purchase is not null && IsPaid(purchase);
        var status = paid ? CourseOrderFinancialStatus.Paid
            : purchase is not null ? CourseOrderFinancialStatus.Processing : orderStatus;
        var delivery = !paid ? CourseOrderDeliveryStatus.NotStarted
            : purchase!.Status == CoursePurchaseStatus.NotificationSent
                ? CourseOrderDeliveryStatus.Delivered : CourseOrderDeliveryStatus.Pending;
        return new CourseOrderItem(externalId, purchase?.Snapshot.ProductTitle ?? title, basePrice,
            purchase?.Snapshot.PriceInNoorAtPurchaseTime ?? allocation,
            purchase?.Id.Value, purchase?.LedgerTransactionId, purchase?.Snapshot.PurchasedAt,
            status.ToString(), delivery.ToString(),
            (purchase?.Snapshot.ItemType ?? itemType ?? ProductItemType.Course).ToString(),
            purchase?.Snapshot.ExternalParentId ?? parentExternalId);
    }

    private static bool CompletePurchases(string[] ids, CoursePurchase[] purchases) => ids.Length > 0 &&
        purchases.Length == ids.Length && ids.All(id => purchases.Count(purchase =>
            purchase.Snapshot.ExternalProductId == id && IsPaid(purchase)) == 1);

    private static bool IsPaid(CoursePurchase purchase) => purchase.Status is
        CoursePurchaseStatus.Debited or CoursePurchaseStatus.NotificationSent or CoursePurchaseStatus.NotificationFailed;

    private static bool NeedsReview(TopUpRequest? topUp) => topUp is not null &&
        (topUp.Status == TopUpStatus.Unresolved ||
         topUp.GatewayResultKind is GatewayResultKinds.Unknown or GatewayResultKinds.ReverseFailed ||
         topUp.ReviewOpenedAtUtc.HasValue && !topUp.ReviewResolvedAtUtc.HasValue);

    private static CourseOrderDeliveryStatus Delivery(IReadOnlyList<CourseOrderItem> items)
    {
        var delivered = items.Count(item => item.DeliveryStatus == CourseOrderDeliveryStatus.Delivered.ToString());
        if (items.Count > 0 && delivered == items.Count)
        {
            return CourseOrderDeliveryStatus.Delivered;
        }
        if (delivered > 0)
        {
            return CourseOrderDeliveryStatus.PartiallyDelivered;
        }
        return items.Any(item => item.FinancialStatus == CourseOrderFinancialStatus.Paid.ToString())
            ? CourseOrderDeliveryStatus.Pending : CourseOrderDeliveryStatus.NotStarted;
    }

    private static IEnumerable<Guid> TopUpIds(CourseCart cart) => cart.TopUpRequestIds
        .Concat(cart.TopUpRequestId.HasValue ? [cart.TopUpRequestId.Value] : Array.Empty<Guid>()).Distinct();

    private static DateTime PurchaseUpdatedAt(CoursePurchase purchase) => new[]
    {
        purchase.CreatedAtUtc, purchase.UpdatedAt ?? purchase.CreatedAtUtc,
        purchase.Snapshot.PurchasedAt, purchase.CompletedAtUtc ?? purchase.CreatedAtUtc,
        purchase.LastNotificationAttemptAtUtc ?? purchase.CreatedAtUtc
    }.Max();

    private static DateTime UpdatedAt(DateTime source, CoursePurchase[] purchases, TopUpRequest[] topUps) =>
        purchases.Select(PurchaseUpdatedAt).Concat(topUps.Select(topUp => new[]
        {
            topUp.CreatedAtUtc, topUp.UpdatedAt ?? topUp.CreatedAtUtc,
            topUp.ConfirmedAtUtc ?? topUp.CreatedAtUtc, topUp.RejectedAtUtc ?? topUp.CreatedAtUtc,
            topUp.GatewayResultAtUtc ?? topUp.CreatedAtUtc, topUp.ReviewOpenedAtUtc ?? topUp.CreatedAtUtc,
            topUp.ReviewResolvedAtUtc ?? topUp.CreatedAtUtc
        }.Max())).Append(source).Max();

    private static CourseOrderTopUp[] MapTopUps(IEnumerable<TopUpRequest> topUps) => topUps
        .OrderBy(topUp => topUp.CreatedAtUtc).ThenBy(topUp => topUp.Id.Value)
        .Select(topUp => new CourseOrderTopUp(topUp.Id.Value, topUp.Status.ToString(),
            topUp.AmountNoor, topUp.AmountRial, topUp.RialPerNoorSnapshot, topUp.ClientReferenceCode,
            topUp.CreatedAtUtc, topUp.ConfirmedAtUtc, topUp.GatewayResultKind, topUp.ReviewReasonCode)).ToArray();
}
