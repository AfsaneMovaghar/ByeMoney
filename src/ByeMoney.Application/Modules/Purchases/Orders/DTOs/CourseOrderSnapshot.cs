using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderSnapshot(
    IReadOnlyList<Cart> Carts, IReadOnlyList<TopUpRequest> TopUps,
    IReadOnlyList<CoursePurchase> Purchases, IReadOnlyList<CourseOrderBuyer> Buyers);
