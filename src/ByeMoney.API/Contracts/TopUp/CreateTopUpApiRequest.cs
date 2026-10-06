namespace ByeMoney.API.Contracts.TopUp;

using ByeMoney.Domain.Modules.Wallet.TopUps;

public record CreateTopUpApiRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")] decimal AmountNoor,
    PaymentMethod PaymentMethod = PaymentMethod.Gateway,
    string? ExternalTransactionId = null,
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null);

