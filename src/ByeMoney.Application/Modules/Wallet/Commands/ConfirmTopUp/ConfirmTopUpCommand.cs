using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public record ConfirmTopUpCommand(
    TopUpRequestId TopUpRequestId,
    string ExternalTransactionId,
    decimal ConfirmedAmountNoor,
    string? ClientReferenceCode = null,
    string? GatewayName = null,
    string? BankReferenceNumber = null,
    decimal? OriginalAmountRial = null,
    decimal? AffectiveAmountRial = null
) : IRequest<Result>;

