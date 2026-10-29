using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Application.Modules.Wallet.Services;

public sealed class NoorCreditService(
    IUserWalletProvisioningService provisioningService,
    IWalletPostingService postingService) : INoorCreditService
{
    public async Task<NoorCreditResult> CreditAsync(NoorCreditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountNoor <= 0)
        {
            throw new DomainException(DomainErrors.Wallet_CreditAmountMustBePositive);
        }

        var userWallet = await provisioningService.GetOrCreateUserWalletAsync(request.UserId, ct);
        var systemAccount = await provisioningService.GetOrCreateSystemAccountAsync(ct);
        var transactionId = Guid.NewGuid();

        await postingService.PostCreditAsync(
            userWallet,
            systemAccount,
            request.AmountNoor,
            request.BalanceType,
            transactionId,
            request.ReferenceType,
            request.ReferenceId,
            request.PerformedByUserId,
            ct);

        return new NoorCreditResult(
            transactionId,
            request.AmountNoor,
            userWallet.Wallet.WithdrawableBalance,
            userWallet.Wallet.GiftBalance,
            userWallet);
    }
}

