namespace ByeMoney.API.Contracts.TopUp;

using ByeMoney.Domain.Modules.Wallet.TopUps;

public record CreateTopUpApiRequest(
    decimal Amount,
    PaymentMethod PaymentMethod = PaymentMethod.Gateway,
    string? ExternalTransactionId = null,
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null);

