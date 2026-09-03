using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface ITopUpRequestRepository : IRepository<TopUpRequest, TopUpRequestId>
{
    Task<TopUpRequest?> GetByExternalTransactionIdAsync(string externalTransactionId, CancellationToken ct = default);
}

