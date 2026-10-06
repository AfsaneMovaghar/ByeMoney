using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public record CreateTopUpRequestCommand(
    Guid UserId,
    decimal AmountNoor,
    PaymentMethod PaymentMethod,
    string? ExternalTransactionId = null,
    IReadOnlyList<PendingItemSnapshot>? PendingItems = null) : IRequest<CreateTopUpRequestResponse>;

