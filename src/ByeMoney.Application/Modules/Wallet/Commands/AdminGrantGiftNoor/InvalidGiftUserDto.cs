namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed record InvalidGiftUserDto(
    string ExternalUserId,
    string Reason);

