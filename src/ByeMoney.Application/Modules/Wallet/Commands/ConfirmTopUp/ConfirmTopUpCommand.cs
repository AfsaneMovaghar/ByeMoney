using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public record ConfirmTopUpCommand(
    Guid TopUpRequestId,
    string? ExternalTransactionId = null) : IRequest<bool>;

