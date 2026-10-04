namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayResultRequest(
    string ClientReferenceCode,
    string Gateway,
    string EventId,
    string Kind,
    string? BankTransactionId,
    string? BankReferenceNumber,
    string? BankResultCode,
    decimal? OriginalAmountRial,
    decimal? AffectiveAmountRial,
    DateTime OccurredAtUtc);
