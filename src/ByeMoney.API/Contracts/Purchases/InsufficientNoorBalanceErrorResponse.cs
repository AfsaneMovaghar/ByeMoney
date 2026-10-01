using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.API.Contracts.Purchases;

public sealed record InsufficientNoorBalanceErrorResponse(
    string ErrorCode,
    decimal CurrentBalanceInNoor,
    decimal PriceInNoor,
    decimal ShortfallInNoor,
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null);

