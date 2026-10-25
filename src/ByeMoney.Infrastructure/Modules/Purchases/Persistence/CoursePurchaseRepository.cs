using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class CoursePurchaseRepository : BaseRepository<CoursePurchase, CoursePurchaseId>, ICoursePurchaseRepository
{
    public CoursePurchaseRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default)
    {
        return await DbSet
            .AnyAsync(cp => cp.BuyerId == buyerId &&
                (cp.Snapshot.ExternalProductId == externalCourseId ||
                 (cp.Snapshot.ExternalParentId != null && cp.Snapshot.ExternalParentId == externalCourseId)), ct);
    }

    public async Task<bool> HasPurchasedAnyAsync(
        UserId buyerId,
        IReadOnlyCollection<string> productIds,
        IReadOnlyCollection<string>? parentIds = null,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0 && (parentIds == null || parentIds.Count == 0))
        {
            return false;
        }

        var query = DbSet.Where(cp => cp.BuyerId == buyerId);

        if (parentIds != null && parentIds.Count > 0)
        {
            return await query.AnyAsync(cp =>
                productIds.Contains(cp.Snapshot.ExternalProductId) ||
                (cp.Snapshot.ExternalParentId != null && productIds.Contains(cp.Snapshot.ExternalParentId)) ||
                (cp.Snapshot.ItemType == ProductItemType.Course && parentIds.Contains(cp.Snapshot.ExternalProductId)), ct);
        }

        return await query.AnyAsync(cp =>
            productIds.Contains(cp.Snapshot.ExternalProductId) ||
            (cp.Snapshot.ExternalParentId != null && productIds.Contains(cp.Snapshot.ExternalParentId)), ct);
    }

    public async Task<Result> ExecutePurchaseTransactionAsync(
        UserId buyerId,
        Func<Task> executeChangesAsync,
        CancellationToken ct = default)
    {
        try
        {
            IDbContextTransaction? tx = null;
            if (Context.Database.IsRelational())
            {
                tx = await Context.Database.BeginTransactionAsync(ct);
                if (Context.Database.IsNpgsql())
                {
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(1869767798, hashtext({buyerId.Value.ToString()}))", ct);
                }
            }

            await executeChangesAsync();

            await Context.SaveChangesAsync(ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
                await tx.DisposeAsync();
            }

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict(ApplicationErrors.CoursePurchase_AlreadyPurchased, PurchaseErrorCodes.AlreadyPurchased);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Result.Conflict(ApplicationErrors.CoursePurchase_AlreadyPurchased, PurchaseErrorCodes.AlreadyPurchased);
        }
    }

    public Task<CoursePurchase?> GetByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Where(cp => cp.BuyerId == buyerId && cp.Snapshot.ExternalProductId == externalCourseId)
            .OrderByDescending(cp => cp.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<List<CoursePurchase>> GetFailedNotificationsForRetryAsync(int maxAttempts, int batchSize, CancellationToken ct = default)
    {
        var staleDebitedThreshold = DateTime.UtcNow.AddMinutes(-2);
        return await DbSet
            .Where(cp => cp.CartId == null)
            .Where(cp => (cp.Status == CoursePurchaseStatus.NotificationFailed || (cp.Status == CoursePurchaseStatus.Debited && cp.CreatedAtUtc < staleDebitedThreshold)) && cp.NotificationAttempts < maxAttempts)
            .OrderBy(cp => cp.LastNotificationAttemptAtUtc ?? cp.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}

