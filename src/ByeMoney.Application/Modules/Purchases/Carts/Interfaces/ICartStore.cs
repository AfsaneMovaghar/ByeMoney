using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

namespace ByeMoney.Application.Modules.Purchases.Carts.Interfaces;

public interface ICartStore
{
    Task<Cart?> FindAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<Cart?> FindByTopUpAsync(Guid topUpId, Guid buyerId, CancellationToken ct);
    Task<Result<Cart>> CreateAsync(Cart cart, CancellationToken ct);
    Task<Result<Cart>> CancelAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task<Result<Cart>> ReopenAsync(Guid id, Guid buyerId, Guid reservationId, CancellationToken ct);
    Task<Cart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CartQuoteItem> items, Guid reservationId, CancellationToken ct);
    Task<Cart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CartQuoteItem> items, decimal shippingAmountInNoor, string? shippingRuleLabel, Guid reservationId, CancellationToken ct);
    Task<Result<Cart>> PayAsync(
        Guid id,
        Guid buyerId,
        Guid reservationId,
        CancellationToken ct,
        decimal? referenceRate = null);
    Task<Result<CreateTopUpRequestResponse>> CreateTopUpAsync(Guid id, Guid buyerId, CancellationToken ct);
    Task FailAsync(Guid id, Guid buyerId, bool expired, Guid reservationId, CancellationToken ct);
    Task RecordSynchronizationAsync(Guid id, Guid buyerId, bool succeeded, string? errorCode, Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<Cart>> GetPendingAsync(CancellationToken ct);
}

