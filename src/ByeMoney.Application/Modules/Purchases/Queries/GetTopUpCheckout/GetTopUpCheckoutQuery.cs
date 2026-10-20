using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetTopUpCheckout;

public sealed record GetTopUpCheckoutQuery(Guid TopUpId) : IRequest<TopUpCheckoutResponse>;
