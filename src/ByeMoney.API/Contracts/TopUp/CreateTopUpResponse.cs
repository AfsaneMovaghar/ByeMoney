namespace ByeMoney.API.Contracts.TopUp;

public record CreateTopUpResponse(
    Guid TopUpRequestId,
    string ClientReferenceCode)
{
    public string ClientReferenceId => ClientReferenceCode;
}

