using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class CoursePurchaseRepository : BaseRepository<CoursePurchase, CoursePurchaseId>, ICoursePurchaseRepository
{
    public CoursePurchaseRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default)
    {
        return await DbSet
            .AnyAsync(cp => cp.BuyerId == buyerId && cp.Snapshot.ExternalProductId == externalCourseId, ct);
    }

    public async Task<List<CoursePurchase>> GetFailedNotificationsForRetryAsync(int maxAttempts, int batchSize, CancellationToken ct = default)
    {
        return await DbSet
            .Where(cp => cp.Status == CoursePurchaseStatus.NotificationFailed && cp.NotificationAttempts < maxAttempts)
            .OrderBy(cp => cp.LastNotificationAttemptAtUtc ?? cp.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}