using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public record CreateTopUpRequestCommand(
    Guid UserId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? ExternalTransactionId = null,
    PendingItemType? PendingItemType = null,
    string? PendingItemExternalId = null,
    decimal? PendingPriceSnapshot = null,
    decimal? PendingRateSnapshot = null) : IRequest<CreateTopUpRequestResponse>;

