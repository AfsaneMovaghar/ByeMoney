using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;

public sealed record RecordGatewayResultCommand(
    string ClientReferenceCode,
    string Gateway,
    string EventId,
    string Kind,
    string? BankTransactionId,
    string? BankReferenceNumber,
    string? BankResultCode,
    decimal? OriginalAmountRial,
    decimal? AffectiveAmountRial,
    DateTime OccurredAtUtc) : IRequest<Result>;
