namespace ByeMoney.API.Contracts.Gift;

public sealed record AdminGrantGiftNoorApiRequest(
    IReadOnlyList<string> ExternalUserIds,
    decimal AmountNoor,
    string Reason);

