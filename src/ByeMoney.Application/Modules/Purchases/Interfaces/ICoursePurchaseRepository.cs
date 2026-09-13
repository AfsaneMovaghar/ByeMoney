using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Interfaces;

public interface ICoursePurchaseRepository
{
    Task<bool> ExistsByBuyerAndCourseAsync(UserId buyerId, string externalCourseId, CancellationToken ct = default);
    Task<CoursePurchase?> GetByIdAsync(CoursePurchaseId id, CancellationToken ct = default);
    Task<List<CoursePurchase>> GetFailedNotificationsForRetryAsync(int maxAttempts, int batchSize, CancellationToken ct = default);
    Task AddAsync(CoursePurchase purchase, CancellationToken ct = default);
    void Update(CoursePurchase purchase);
}
