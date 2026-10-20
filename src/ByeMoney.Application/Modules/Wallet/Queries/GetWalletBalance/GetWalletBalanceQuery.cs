using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public record GetWalletBalanceQuery : IRequest<WalletBalanceResponse>;
