using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public record InsufficientBalanceFailureState(
    decimal CurrentBalanceInNoor,
    decimal PriceInNoor,
    decimal ShortfallInNoor,
    string ErrorCode = PurchaseErrorCodes.InsufficientNoorBalance,
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null);

