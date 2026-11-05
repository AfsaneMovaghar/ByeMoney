using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminPendingPhysicalRestocks;

public sealed class GetAdminPendingPhysicalRestocksQueryHandler(
    IPhysicalCancellationStore store,
    PhysicalRestockSettings settings) :
    IRequestHandler<GetAdminPendingPhysicalRestocksQuery, IReadOnlyList<PendingPhysicalRestockResponse>>
{
    /// <summary>سقف سطرهای خوانده‌شده؛ صف ناتمام در حالت عادی خالی یا بسیار کوچک است و فهرست صفحه‌بندی ندارد.</summary>
    private const int RowLimit = 500;

    public async Task<IReadOnlyList<PendingPhysicalRestockResponse>> Handle(
        GetAdminPendingPhysicalRestocksQuery request, CancellationToken ct)
    {
        var rows = await store.GetUnfinishedRestocksAsync(RowLimit, ct);

        return rows
            .GroupBy(x => x.OperationId)
            .Select(ToResponse)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    private PendingPhysicalRestockResponse ToResponse(IGrouping<Guid, PendingRestockRow> operation)
    {
        var first = operation.First();
        var attempts = operation.Max(x => x.Attempts);

        return new PendingPhysicalRestockResponse(
            operation.Key,
            first.CartId,
            attempts,
            settings.MaxAttempts,
            attempts >= settings.MaxAttempts,
            first.LastErrorCode,
            operation.Min(x => x.CreatedAtUtc),
            operation.Max(x => x.LastAttemptAtUtc),
            operation.Select(x => new PhysicalRestockItem(x.PurchaseId, x.Quantity)).ToArray());
    }
}
