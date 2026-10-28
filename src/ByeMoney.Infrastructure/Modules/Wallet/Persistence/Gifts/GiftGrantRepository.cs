using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Gifts;

public class GiftGrantRepository(ApplicationDbContext context) : IGiftGrantRepository
{
    public async Task<GiftGrantBatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await context.GiftGrantBatches
            .Include(b => b.Recipients)
            .FirstOrDefaultAsync(b => b.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task AddBatchAsync(GiftGrantBatch batch, CancellationToken ct = default)
    {
        await context.GiftGrantBatches.AddAsync(batch, ct);
    }
}

