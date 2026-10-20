using ByeMoney.Application.Modules.Wallet.DTOs;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayReview;

public sealed record GatewayReviewDetails(Guid TopUpRequestId, string ClientReferenceCode,
    DateTime RequestedAtUtc, decimal? AmountRial, string TopUpStatus, string? UserName, string? UserPhone,
    string? RefNum, string? Rrn, string? GatewayResultKind, GatewayReviewState? Review);
