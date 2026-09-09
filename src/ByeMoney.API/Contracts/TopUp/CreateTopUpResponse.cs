namespace ByeMoney.API.Contracts.TopUp;

public record CreateTopUpResponse(
    Guid TopUpRequestId,
    string ClientReferenceId);

