using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface ITopUpSettlementService
{
    Task SettleAsync(TopUpRequest topUp, CancellationToken ct = default);
}
