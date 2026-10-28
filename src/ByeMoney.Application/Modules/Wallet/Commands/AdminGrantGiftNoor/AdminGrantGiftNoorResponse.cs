namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed record AdminGrantGiftNoorResponse(
    Guid BatchId,
    decimal AmountNoorPerUser,
    int TotalRecipients,
    decimal TotalAmountNoor,
    string Reason,
    DateTime GrantedAtUtc,
    IReadOnlyList<GiftRecipientResponseDto> Recipients);

