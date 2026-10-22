using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Interfaces;

public interface ICoursePurchaseNotifier
{
    Task NotifyAsync(
        CoursePurchase purchase,
        string buyerExternalUserId,
        ProductSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
