namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayConfirmationRequest(
    string ClientReferenceCode,
    string Gateway,
    string ExternalTransactionId,
    string BankReferenceNumber,
    decimal OriginalAmountRial,
    decimal AffectiveAmountRial);
