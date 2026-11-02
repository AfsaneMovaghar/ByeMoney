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
        var physicalByCart = snapshot.PhysicalPurchases.ToLookup(purchase => purchase.CartId);
        var groupedPurchases = new HashSet<Guid>();
        var groupedPhysicalPurchases = new HashSet<Guid>();

        foreach (var cart in snapshot.Carts)
        {
            var purchases = purchasesByCart[cart.Id].ToArray();
            var physicalPurchases = physicalByCart[cart.Id].ToArray();
            var ids = TopUpIds(cart).ToArray();
            var topUps = ids.Where(topUpsById.ContainsKey).Select(id => topUpsById[id]).ToArray();
            var invalidLink = topUps.Length != ids.Length ||
                topUps.Any(topUp => topUp.UserId.Value != cart.BuyerId) ||
                purchases.Any(purchase => purchase.BuyerId.Value != cart.BuyerId) ||
                physicalPurchases.Any(purchase => purchase.BuyerId.Value != cart.BuyerId);
            var ownedTopUps = topUps.Where(topUp => topUp.UserId.Value == cart.BuyerId).ToArray();
            var ownedPurchases = purchases.Where(purchase => purchase.BuyerId.Value == cart.BuyerId).ToArray();
            var ownedPhysicalPurchases = physicalPurchases
                .Where(purchase => purchase.BuyerId.Value == cart.BuyerId).ToArray();
            groupedPurchases.UnionWith(ownedPurchases.Select(purchase => purchase.Id.Value));
            groupedPhysicalPurchases.UnionWith(ownedPhysicalPurchases.Select(purchase => purchase.Id.Value));
            rows.Add(new CourseOrderReadRow(cart.BuyerId,
                MapCart(cart, ownedPurchases, ownedPhysicalPurchases, ownedTopUps, invalidLink)));
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

        foreach (var physical in snapshot.PhysicalPurchases
            .Where(purchase => !groupedPhysicalPurchases.Contains(purchase.Id.Value)))
        {
            var item = MapPhysicalItem(physical, null);
            rows.Add(new CourseOrderReadRow(physical.BuyerId.Value, new CourseOrderDetails(
                CourseOrderId.For(CourseOrderSourceType.Purchase, physical.Id.Value),
                CourseOrderSourceType.Purchase.ToString(), physical.CartId, physical.CreatedAtUtc,
                PhysicalPurchaseUpdatedAt(physical), CourseOrderFinancialStatus.RequiresReview.ToString(),
                Delivery([item]).ToString(), physical.PaidAmountInNoor, 1, null, null, null, null, [item], [])));
        }

        return rows;
    }

    private static CourseOrderDetails MapCart(
        Cart cart, CoursePurchase[] purchases, PhysicalProductPurchase[] physicalPurchases,
        TopUpRequest[] topUps, bool invalidLink)
    {
        var requested = RequestedItems(cart);
        var expectedKeys = requested.Select(item => new OrderItemKey(item.ExternalId, item.Type)).Distinct().ToArray();
        var purchasedKeys = purchases.Select(KeyOf).Concat(physicalPurchases.Select(KeyOf)).ToArray();
        var complete = CompletePurchases(expectedKeys, purchases, physicalPurchases);
        var hasPurchases = purchases.Any(IsPaid) || physicalPurchases.Any(IsPaid);
        var consistent = !invalidLink &&
            purchasedKeys.All(expectedKeys.Contains) &&
            purchasedKeys.GroupBy(key => key).All(group => group.Count() == 1) &&
            purchases.Where(IsPaid).All(purchase => MatchesCartItem(cart, purchase)) &&
            physicalPurchases.Where(IsPaid).All(purchase => MatchesCartItem(cart, purchase)) &&
            (cart.Status != CartStatus.AwaitingTopUp || cart.TopUpRequestId.HasValue);
        var current = topUps.SingleOrDefault(topUp => topUp.Id.Value == cart.TopUpRequestId);
        var status = CartFinancialStatus(cart, complete, hasPurchases, consistent, current);
        var keys = expectedKeys.Concat(purchasedKeys).Distinct()
            .OrderBy(key => key.ExternalId, StringComparer.Ordinal).ThenBy(key => key.Type).ToArray();
        var items = keys.Select(key =>
        {
            var quote = FindCartItem(cart, key);
            var physical = key.Type == ProductItemType.Product
                ? physicalPurchases.FirstOrDefault(item => KeyOf(item) == key)
                : null;
            if (physical is not null)
            {
                return MapPhysicalItem(physical, quote);
            }

            var purchase = purchases.FirstOrDefault(item => KeyOf(item) == key);
            var requestedItem = requested.FirstOrDefault(
                item => new OrderItemKey(item.ExternalId, item.Type) == key);
            return MapItem(key.ExternalId, quote?.Title, quote?.PriceInNoor, purchase, status,
                quote?.AllocatedAmountInNoor, key.Type, quote?.ParentExternalId,
                quote?.Quantity ?? requestedItem?.Quantity ?? 1);
        }).ToArray();
        var hasQuote = cart.Items.Count > 0 && expectedKeys.All(key => FindCartItem(cart, key) is not null);
        var summary = hasQuote ? new PurchaseCartSummary(
            cart.OriginalTotal, cart.ItemDiscountInNoor, cart.CouponDiscountInNoor,
            cart.Total, cart.ShippingAmountInNoor) : null;
        return new CourseOrderDetails(
            CourseOrderId.For(CourseOrderSourceType.Cart, cart.Id), CourseOrderSourceType.Cart.ToString(),
            cart.Id, cart.CreatedAtUtc, UpdatedAt(cart.UpdatedAtUtc, purchases, physicalPurchases, topUps),
            status.ToString(), Delivery(items).ToString(), summary?.FinalAmountInNoor, expectedKeys.Length,
            cart.Status.ToString(), cart.ExpiresAtUtc, cart.TopUpRequestId, summary, items, MapTopUps(topUps));
    }

    private static OrderItemKey KeyOf(CoursePurchase purchase) =>
        new(purchase.Snapshot.ExternalProductId, purchase.Snapshot.ItemType);

    private static OrderItemKey KeyOf(PhysicalProductPurchase purchase) =>
        new(purchase.ExternalProductId, ProductItemType.Product);

    private static CartItem? FindCartItem(Cart cart, OrderItemKey key) => cart.Items.FirstOrDefault(
        item => item.ExternalCourseId == key.ExternalId && item.ItemType == key.Type);

    private static bool MatchesCartItem(Cart cart, CoursePurchase purchase)
    {
        var item = FindCartItem(cart, KeyOf(purchase));
        return item is not null && item.AllocatedAmountInNoor == purchase.Snapshot.PriceInNoorAtPurchaseTime;
    }

    private static bool MatchesCartItem(Cart cart, PhysicalProductPurchase purchase)
    {
        var item = FindCartItem(cart, KeyOf(purchase));
        return item is not null &&
            item.Quantity == purchase.Quantity &&
            item.PriceInNoor == purchase.UnitPriceInNoor &&
            item.AllocatedAmountInNoor == purchase.PaidAmountInNoor;
    }

    private static CartItemInput[] RequestedItems(Cart cart) =>
        cart.Items.Count > 0
            ? cart.Items.Select(item => new CartItemInput(item.ExternalCourseId, item.ItemType, item.Quantity)).ToArray()
            : cart.RequestedItemSignatures.Select(CartItemSignature.Parse).ToArray();

    private static CourseOrderFinancialStatus CartFinancialStatus(
        Cart cart, bool complete, bool hasPurchases, bool consistent, TopUpRequest? current)
    {
        if (consistent && complete && cart.Status == CartStatus.Paid)
        {
            return CourseOrderFinancialStatus.Paid;
        }
        if (!consistent || hasPurchases || cart.Status == CartStatus.Paid || NeedsReview(current))
        {
            return CourseOrderFinancialStatus.RequiresReview;
        }

        return cart.Status switch
        {
            CartStatus.Failed => CourseOrderFinancialStatus.Failed,
            CartStatus.Expired => CourseOrderFinancialStatus.Expired,
            CartStatus.Preparing => CourseOrderFinancialStatus.Preparing,
            CartStatus.AwaitingTopUp when current?.Status == TopUpStatus.Confirmed => CourseOrderFinancialStatus.Processing,
            CartStatus.AwaitingTopUp when current?.Status is TopUpStatus.Rejected or TopUpStatus.ManuallyRefunded => CourseOrderFinancialStatus.Failed,
            _ => CourseOrderFinancialStatus.AwaitingPayment
        };
    }

    private static CourseOrderDetails MapTopUp(
        TopUpRequest topUp, PendingItemSnapshot[] pending, CoursePurchase[] purchases)
    {
        var expectedKeys = pending.Select(item => new OrderItemKey(item.ExternalId, ProductItemType.Course)).ToArray();
        var consistent = purchases.All(purchase => pending.Any(item =>
            item.ExternalId == purchase.Snapshot.ExternalProductId &&
            item.PriceNoorSnapshot == purchase.Snapshot.PriceInNoorAtPurchaseTime));
        var complete = consistent && CompletePurchases(expectedKeys, purchases, []);
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
            UpdatedAt(topUp.UpdatedAt ?? topUp.CreatedAtUtc, purchases, [], [topUp]),
            status.ToString(), Delivery(items).ToString(), items.Sum(item => item.FinalAmountInNoor),
            items.Length, null, null, topUp.Id.Value, null, items, MapTopUps([topUp]));
    }

    private static CourseOrderItem MapItem(
        string externalId, string? title, decimal? basePrice, CoursePurchase? purchase,
        CourseOrderFinancialStatus orderStatus, decimal? allocatedAmount = null,
        ProductItemType? itemType = null, string? parentExternalId = null, int quantity = 1)
    {
        var paid = purchase is not null && IsPaid(purchase);
        var status = paid ? CourseOrderFinancialStatus.Paid
            : purchase is not null ? CourseOrderFinancialStatus.Processing : orderStatus;
        var delivery = !paid ? CourseOrderDeliveryStatus.NotStarted
            : purchase!.Status == CoursePurchaseStatus.NotificationSent
                ? CourseOrderDeliveryStatus.Delivered : CourseOrderDeliveryStatus.Pending;
        return new CourseOrderItem(externalId, purchase?.Snapshot.ProductTitle ?? title, basePrice,
            purchase?.Snapshot.PriceInNoorAtPurchaseTime ?? allocatedAmount,
            purchase?.Id.Value, purchase?.LedgerTransactionId, purchase?.Snapshot.PurchasedAt,
            status.ToString(), delivery.ToString(),
            (purchase?.Snapshot.ItemType ?? itemType ?? ProductItemType.Course).ToString(),
            purchase?.Snapshot.ExternalParentId ?? parentExternalId, quantity);
    }

    private static CourseOrderItem MapPhysicalItem(PhysicalProductPurchase physical, CartItem? quote)
    {
        var paid = IsPaid(physical);
        var status = paid ? CourseOrderFinancialStatus.Paid : CourseOrderFinancialStatus.Processing;
        var delivery = !paid ? CourseOrderDeliveryStatus.NotStarted
            : physical.DeliveryStatus == PhysicalDeliveryStatus.Delivered
                ? CourseOrderDeliveryStatus.Delivered : CourseOrderDeliveryStatus.Pending;
        return new CourseOrderItem(physical.ExternalProductId, physical.ProductTitle,
            quote?.PriceInNoor ?? physical.UnitPriceInNoor, physical.PaidAmountInNoor,
            physical.Id.Value, physical.LedgerTransactionId, physical.CreatedAtUtc,
            status.ToString(), delivery.ToString(), nameof(ProductItemType.Product), null,
            physical.Quantity, physical.DeliveryStatus.ToString());
    }

    private static bool CompletePurchases(
        OrderItemKey[] keys, CoursePurchase[] purchases, PhysicalProductPurchase[] physicalPurchases)
    {
        var records = purchases.Select(purchase => (Key: KeyOf(purchase), Paid: IsPaid(purchase)))
            .Concat(physicalPurchases.Select(purchase => (Key: KeyOf(purchase), Paid: IsPaid(purchase))))
            .ToArray();
        return keys.Length > 0 && records.Length == keys.Length &&
            keys.All(key => records.Count(record => record.Key == key && record.Paid) == 1);
    }

    private readonly record struct OrderItemKey(string ExternalId, ProductItemType Type);

    private static bool IsPaid(CoursePurchase purchase) => purchase.Status is
        CoursePurchaseStatus.Debited or CoursePurchaseStatus.NotificationSent or CoursePurchaseStatus.NotificationFailed;

    private static bool IsPaid(PhysicalProductPurchase purchase) => purchase.LedgerTransactionId.HasValue;

    private static bool NeedsReview(TopUpRequest? topUp) => topUp?.IsUnderReview ?? false;





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

    private static IEnumerable<Guid> TopUpIds(Cart cart) => cart.TopUpRequestIds
        .Concat(cart.TopUpRequestId.HasValue ? [cart.TopUpRequestId.Value] : Array.Empty<Guid>()).Distinct();

    private static DateTime PurchaseUpdatedAt(CoursePurchase purchase) => new[]
    {
        purchase.CreatedAtUtc, purchase.UpdatedAt ?? purchase.CreatedAtUtc,
        purchase.Snapshot.PurchasedAt, purchase.CompletedAtUtc ?? purchase.CreatedAtUtc,
        purchase.LastNotificationAttemptAtUtc ?? purchase.CreatedAtUtc
    }.Max();

    private static DateTime PhysicalPurchaseUpdatedAt(PhysicalProductPurchase purchase) => new[]
    {
        purchase.CreatedAtUtc, purchase.UpdatedAt ?? purchase.CreatedAtUtc,
        purchase.ShippedAtUtc ?? purchase.CreatedAtUtc, purchase.DeliveredAtUtc ?? purchase.CreatedAtUtc
    }.Max();

    private static DateTime UpdatedAt(
        DateTime source, CoursePurchase[] purchases, PhysicalProductPurchase[] physicalPurchases,
        TopUpRequest[] topUps) =>
        purchases.Select(PurchaseUpdatedAt).Concat(physicalPurchases.Select(PhysicalPurchaseUpdatedAt))
            .Concat(topUps.Select(topUp => new[]
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
