namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public record PendingItemSnapshot(
    PendingItemType ItemType,
    string ExternalId,
    decimal PriceSnapshot,
    decimal RateSnapshot);

