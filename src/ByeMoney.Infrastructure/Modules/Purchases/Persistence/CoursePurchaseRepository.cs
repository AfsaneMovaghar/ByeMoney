using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class CoursePurchaseRepository : ICoursePurchaseRepository
{
    private readonly ApplicationDbContext _context;

    public CoursePurchaseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default)
    {
        return await _context.Set<CoursePurchase>()
            .AnyAsync(cp => cp.BuyerId == buyerId && cp.Snapshot.ExternalProductId == externalCourseId, ct);
    }

    public async Task<CoursePurchase?> GetByIdAsync(CoursePurchaseId id, CancellationToken ct = default)
    {
        return await _context.Set<CoursePurchase>()
            .FirstOrDefaultAsync(cp => cp.Id == id, ct);
    }

    public async Task<List<CoursePurchase>> GetFailedNotificationsForRetryAsync(int maxAttempts, int batchSize, CancellationToken ct = default)
    {
        return await _context.Set<CoursePurchase>()
            .Where(cp => cp.Status == CoursePurchaseStatus.NotificationFailed && cp.NotificationAttempts < maxAttempts)
            .OrderBy(cp => cp.LastNotificationAttemptAtUtc ?? cp.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CoursePurchase purchase, CancellationToken ct = default)
    {
        await _context.Set<CoursePurchase>().AddAsync(purchase, ct);
    }

    public void Update(CoursePurchase purchase)
    {
        _context.Set<CoursePurchase>().Update(purchase);
    }
}