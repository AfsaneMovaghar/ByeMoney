namespace ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;

public record CreateTopUpRequestResponse(Guid TopUpRequestId, string ClientReferenceId, decimal AmountNoor, decimal RialPerNoor);