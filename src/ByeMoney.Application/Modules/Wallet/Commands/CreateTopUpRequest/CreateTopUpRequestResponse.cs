namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public record CreateTopUpRequestResponse(
    Guid TopUpRequestId,
    string ClientReferenceId);
