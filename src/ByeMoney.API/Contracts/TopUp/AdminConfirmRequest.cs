namespace ByeMoney.API.Contracts.TopUp;

public record AdminConfirmRequest(string ExternalTransactionId, [property: System.Text.Json.Serialization.JsonPropertyName("confirmedAmount")] decimal ConfirmedAmountNoor);
