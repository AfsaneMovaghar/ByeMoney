namespace ByeMoney.Application.Modules.Wallet.Constants;

public static class GatewayTopUpErrorCodes
{
    public const string TopUpNotFound = "TOPUP_NOT_FOUND";
    public const string RefNumConflict = "REFNUM_CONFLICT";
    public const string TopUpAmountMismatch = "TOPUP_AMOUNT_MISMATCH";
    public const string TopUpRequiresReview = "TOPUP_REQUIRES_REVIEW";
    public const string InvalidVerificationData = "INVALID_VERIFICATION_DATA";
    public const string TopUpConcurrentConfirmation = "TOPUP_CONCURRENT_CONFIRMATION";
    public const string TopUpAlreadyConfirmed = "TOPUP_ALREADY_CONFIRMED";
}
