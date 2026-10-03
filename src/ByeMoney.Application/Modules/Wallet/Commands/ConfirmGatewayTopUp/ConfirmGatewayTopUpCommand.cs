using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;

public sealed record ConfirmGatewayTopUpCommand(
    string ClientReferenceCode,
    string Gateway,
    string ExternalTransactionId,
    string BankReferenceNumber,
    decimal OriginalAmountRial,
    decimal AffectiveAmountRial) : IRequest<GatewayConfirmationOutcome>;

public sealed record GatewayConfirmationOutcome(Result Result, bool Idempotent = false);
