using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public record InsufficientBalanceFailureState(
    decimal CurrentBalanceInNoor,
    decimal PriceInNoor,
    decimal ShortfallInNoor,
    decimal ShortfallInRial,
    string ErrorCode = "INSUFFICIENT_NOOR_BALANCE",
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null);

