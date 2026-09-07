using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

public class GetWalletBalanceQueryHandler : IRequestHandler<GetWalletBalanceQuery, WalletBalanceResponse>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetWalletBalanceQueryHandler(
        IWalletRepository walletRepository,
        ICurrentUserService currentUserService)
    {
        _walletRepository = walletRepository;
        _currentUserService = currentUserService;
    }

    public async Task<WalletBalanceResponse> Handle(GetWalletBalanceQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);
        }

        var wallet = await _walletRepository.GetByUserIdAsync(new UserId(currentUserId.Value), cancellationToken);
        if (wallet is null)
        {
            return new WalletBalanceResponse(0m);
        }

        return new WalletBalanceResponse(wallet.Balance);
    }
}

