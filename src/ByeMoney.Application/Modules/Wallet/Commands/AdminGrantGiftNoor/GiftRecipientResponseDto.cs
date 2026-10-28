namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed record GiftRecipientResponseDto(
    string ExternalUserId,
    Guid UserId,
    string Status,
    decimal NewGiftBalance);

