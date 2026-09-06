using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public record ConfirmTopUpCommand(
    TopUpRequestId TopUpRequestId,
    string ExternalTransactionId,
    decimal ConfirmedAmount
) : IRequest<Result>;

