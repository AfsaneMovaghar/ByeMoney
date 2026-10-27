namespace ByeMoney.Domain.Modules.Wallet.TopUps.Policies;

public static class GatewayTopUpExpiryPolicy
{
    public static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMinutes(30);

    public static bool IsEligibleForExpiryReview(TopUpRequest topUp, DateTime utcNow) =>
        topUp.PaymentMethod == PaymentMethod.Gateway &&
        topUp.Status == TopUpStatus.Pending &&
        topUp.GatewayResultKind is null &&
        topUp.ReviewCaseId is null &&
        topUp.CreatedAtUtc <= utcNow - ExpiryThreshold;
}

