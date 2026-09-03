using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public record WalletBalanceDto(
    Guid WalletId,
    Guid AccountId,
    Guid UserId,
    decimal Balance,
    DateTime LastUpdatedAtUtc);

public record GetWalletBalanceQuery(Guid UserId) : IRequest<WalletBalanceDto?>;

