using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;

namespace ByeMoney.Application.Modules.Purchases.Orders.Interfaces;

/// <summary>
/// اجرای تغییر وضعیت تحویل زیر همان قفل‌های خریدار و سبدِ لغو کالای فیزیکی،
/// تا ارسال و لغو یک قلم هرگز هم‌زمان اثر نگذارند.
/// </summary>
public interface IPhysicalDeliveryStore
{
    Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct);

    Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct);
}
