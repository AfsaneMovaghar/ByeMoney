using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.TopUp;

public class TopUpRequestRepository : BaseRepository<TopUpRequest, TopUpRequestId>, ITopUpRequestRepository
{
    public TopUpRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<TopUpRequest?> GetByExternalTransactionIdAsync(string externalTransactionId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.ExternalTransactionId == externalTransactionId, ct);
    }
}

