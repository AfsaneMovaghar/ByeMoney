using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public class GetWalletBalanceQueryHandler : IRequestHandler<GetWalletBalanceQuery, WalletBalanceDto?>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletBalanceQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletBalanceDto?> Handle(GetWalletBalanceQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(new UserId(request.UserId), cancellationToken);
        if (wallet is null)
            return null;

        return new WalletBalanceDto(
            wallet.Id.Value,
            wallet.AccountId.Value,
            wallet.UserId.Value,
            wallet.Balance,
            wallet.LastUpdatedAtUtc);
    }
}

