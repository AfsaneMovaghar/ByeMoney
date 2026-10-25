using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Interfaces;

public interface ICoursePurchaseRepository
{
    Task<CoursePurchase?> GetByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default);
    Task<bool> ExistsByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default);
    Task<bool> HasPurchasedAnyAsync(UserId buyerId, IReadOnlyCollection<string> productIds, IReadOnlyCollection<string>? parentIds = null, CancellationToken ct = default);
    Task<Result> ExecutePurchaseTransactionAsync(UserId buyerId, Func<Task> executeChangesAsync, CancellationToken ct = default);
    Task<CoursePurchase?> GetByIdAsync(CoursePurchaseId id, CancellationToken ct = default);
    Task<List<CoursePurchase>> GetFailedNotificationsForRetryAsync(int maxAttempts, int batchSize, CancellationToken ct = default);
    Task AddAsync(CoursePurchase purchase, CancellationToken ct = default);
    void Update(CoursePurchase purchase);
}

