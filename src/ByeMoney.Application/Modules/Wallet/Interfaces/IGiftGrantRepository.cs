using ByeMoney.Domain.Modules.Wallet.Gifts;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IGiftGrantRepository
{
    Task<GiftGrantBatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task AddBatchAsync(GiftGrantBatch batch, CancellationToken ct = default);
}

