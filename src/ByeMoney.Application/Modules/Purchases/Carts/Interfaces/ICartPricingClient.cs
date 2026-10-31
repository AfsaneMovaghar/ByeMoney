using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Purchases.Carts.Interfaces;

public interface ICartPricingClient
{
    Task<Result<CartQuote>> PrepareAsync(CartQuoteRequest request, CancellationToken ct);
    Task<Result> TransitionAsync(CartTransitionRequest request, string action, CancellationToken ct);
}
