using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.Application.Modules.Wallet.Services;

public interface ITopUpSettlementService
{
    Task SettleAsync(TopUpRequest topUp, CancellationToken ct = default);
}

public sealed class TopUpSettlementService(
    IUserWalletProvisioningService provisioning,
    IWalletRepository walletRepository,
    IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
    IUnitOfWork unitOfWork) : ITopUpSettlementService
{
    public async Task SettleAsync(TopUpRequest topUp, CancellationToken ct = default)
    {
        var user = await provisioning.GetOrCreateUserWalletAsync(topUp.UserId, ct);
        var system = await provisioning.GetOrCreateSystemAccountAsync(ct);
        var transactionId = Guid.NewGuid();
        await ledgerRepository.AddAsync(LedgerEntry.Create(user.Account.Id, topUp.AmountNoor, transactionId, LedgerReferenceType.TopUp, topUp.Id.ToString(), topUp.CreatedByUserId), ct);
        await ledgerRepository.AddAsync(LedgerEntry.Create(system.Id, -topUp.AmountNoor, transactionId, LedgerReferenceType.TopUp, topUp.Id.ToString(), topUp.CreatedByUserId), ct);
        user.Wallet.ApplyCredit(topUp.AmountNoor);
        walletRepository.Update(user.Wallet);
        await unitOfWork.SaveChangesAsync(ct);
    }
}