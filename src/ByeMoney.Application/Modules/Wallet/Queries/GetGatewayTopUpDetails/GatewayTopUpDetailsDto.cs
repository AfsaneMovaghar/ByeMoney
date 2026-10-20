namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;

public sealed record GatewayTopUpDetailsDto(
    Guid TopUpRequestId,
    string ClientReferenceCode,
    decimal? AmountRial,
    string Status,
    string PaymentMethod,
    string? ExternalTransactionId,
    string? BankReferenceNumber,
    string? GatewayName,
    bool HasManualRefund = false);
