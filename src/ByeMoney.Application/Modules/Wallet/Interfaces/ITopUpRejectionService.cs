using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface ITopUpRejectionService
{
    Task<Result> RejectAsync(Guid topUpRequestId, string reason, CancellationToken ct = default);
}
