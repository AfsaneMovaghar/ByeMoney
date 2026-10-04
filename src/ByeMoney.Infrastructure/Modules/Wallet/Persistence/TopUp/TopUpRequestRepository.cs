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

    public async Task<TopUpRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    public async Task<TopUpRequest?> GetByReviewCaseIdAsync(string caseId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(t => t.ReviewCaseId == caseId, ct);

    public async Task<TopUpRequest?> GetByClientReferenceCodeAsync(string clientReferenceCode, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.ClientReferenceCode == clientReferenceCode, ct);
    }
}

