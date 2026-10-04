namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayTopUpDetailsResponse(
    Guid TopUpRequestId,
    string ClientReferenceCode,
    decimal? AmountRial,
    string Status,
    string PaymentMethod,
    string? ExternalTransactionId,
    string? BankReferenceNumber,
    string? GatewayName,
    bool HasManualRefund = false);

