using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayReview;

public sealed record GatewayReviewDetails(Guid TopUpRequestId, string ClientReferenceCode,
    DateTime RequestedAtUtc, decimal? AmountRial, string TopUpStatus, string? UserName, string? UserPhone,
    string? RefNum, string? Rrn, string? GatewayResultKind, GatewayReviewState? Review);

public sealed record GetGatewayReviewQuery(string ClientReferenceCode) : IRequest<GatewayReviewDetails?>;

public sealed class GetGatewayReviewQueryHandler(ITopUpRequestRepository topUps, IUserRepository users)
    : IRequestHandler<GetGatewayReviewQuery, GatewayReviewDetails?>
{
    public async Task<GatewayReviewDetails?> Handle(GetGatewayReviewQuery request, CancellationToken ct)
    {
        var topUp = await topUps.GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
        if (topUp is null || topUp.PaymentMethod != PaymentMethod.Gateway) return null;
        var user = await users.GetByIdAsync(topUp.UserId, ct);
        var review = topUp.ReviewCaseId is null ? null : new GatewayReviewState(topUp.ClientReferenceCode,
            topUp.ReviewCaseId, topUp.Status.ToString(), topUp.ReviewReasonCode!, topUp.ReviewOpenedAtUtc!.Value,
            topUp.ReviewResolvedAtUtc, topUp.ReviewOutcomeCode, topUp.ReviewResolutionFinancialReferenceId,
            topUp.ReviewRevision, topUp.ReviewAudit, topUp.ManualRefundReference);
        return new GatewayReviewDetails(topUp.Id.Value, topUp.ClientReferenceCode, topUp.CreatedAtUtc,
            topUp.AmountRial, topUp.Status.ToString(), user?.DisplayName, user?.Phone,
            topUp.ExternalTransactionId, topUp.BankReferenceNumber, topUp.GatewayResultKind, review);
    }
}
