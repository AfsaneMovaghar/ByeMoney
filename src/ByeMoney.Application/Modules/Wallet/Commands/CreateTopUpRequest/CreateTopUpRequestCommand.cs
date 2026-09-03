using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public record CreateTopUpRequestCommand(
    Guid UserId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? ExternalTransactionId = null) : IRequest<Guid>;

