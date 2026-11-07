using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record OrderSnapshot(
    IReadOnlyList<Cart> Carts, IReadOnlyList<TopUpRequest> TopUps,
    IReadOnlyList<CoursePurchase> Purchases, IReadOnlyList<OrderBuyer> Buyers,
    IReadOnlyList<PhysicalProductPurchase> PhysicalPurchases,
    IReadOnlyList<PhysicalProductCancellation> PhysicalCancellations);
