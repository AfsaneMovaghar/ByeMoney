using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public record WalletBalanceResponse(
    decimal Balance,
    string Currency = "Noor");

public record GetWalletBalanceQuery : IRequest<WalletBalanceResponse>;

