using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

namespace ByeMoney.Application.Modules.Purchases.Cart.Interfaces;

public interface ICourseCartStore
{
    Task<CourseCart?> FindAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<CourseCart?> FindByTopUpAsync(Guid topUpId, Guid buyerId, CancellationToken ct);
    Task<Result<CourseCart>> CreateAsync(CourseCart cart, CancellationToken ct);
    Task<Result<CourseCart>> CancelAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<Result<CourseCart>> ReopenAsync(Guid id, Guid buyerId, Guid reservationId, CancellationToken ct);
    Task<CourseCart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CourseCartQuoteItem> items, Guid reservationId, CancellationToken ct);
    Task<CourseCart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CourseCartQuoteItem> items, decimal shippingAmountInNoor, string? shippingRuleLabel, Guid reservationId, CancellationToken ct);
    Task<Result<CourseCart>> PayAsync(
        Guid id,
        Guid buyerId,
        Guid reservationId,
        CancellationToken ct,
        decimal? referenceRate = null);
    Task<Result<CreateTopUpRequestResponse>> CreateTopUpAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task FailAsync(Guid id, Guid buyerId, bool expired, Guid reservationId, CancellationToken ct);
    Task RecordSynchronizationAsync(Guid id, Guid buyerId, bool succeeded, string? errorCode, Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<CourseCart>> GetPendingAsync(CancellationToken ct);
}

