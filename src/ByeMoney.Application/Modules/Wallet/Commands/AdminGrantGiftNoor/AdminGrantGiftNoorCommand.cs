using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed record AdminGrantGiftNoorCommand(
    IReadOnlyList<string> ExternalUserIds,
    decimal AmountNoor,
    string Reason,
    string IdempotencyKey,
    Guid? AdminUserId) : IRequest<AdminGrantGiftNoorResult>;

