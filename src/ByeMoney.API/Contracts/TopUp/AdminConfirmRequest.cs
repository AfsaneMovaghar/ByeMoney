namespace ByeMoney.API.Contracts.TopUp;

public record AdminConfirmRequest(string ExternalTransactionId, decimal ConfirmedAmount);
