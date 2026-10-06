using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;

public record TopUpRequestDto(
    Guid Id,
    Guid UserId,
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")] decimal AmountNoor,
    string PaymentMethod,
    string ClientReferenceCode,
    string Status,
    string? ExternalTransactionId,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? RejectedAtUtc,
    string? ChargeType,
    Guid? CreatedByUserId,
    decimal? AmountRial,
    decimal? RialPerNoorSnapshot,
    bool HasReceipt);

public record GetTopUpByClientReferenceCodeQuery(string ClientReferenceCode) : IRequest<TopUpRequestDto?>;
