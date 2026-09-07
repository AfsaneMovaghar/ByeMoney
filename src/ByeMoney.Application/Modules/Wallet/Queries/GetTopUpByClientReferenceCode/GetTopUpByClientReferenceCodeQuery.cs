using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;

public record TopUpRequestDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string PaymentMethod,
    string ClientReferenceCode,
    string Status,
    string? ExternalTransactionId,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? RejectedAtUtc);

public record GetTopUpByClientReferenceCodeQuery(string ClientReferenceCode) : IRequest<TopUpRequestDto?>;
