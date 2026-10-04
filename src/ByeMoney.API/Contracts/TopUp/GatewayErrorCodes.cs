namespace ByeMoney.API.Contracts.TopUp;

public static class GatewayErrorCodes
{
    public const string TopUpNotFound = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.TopUpNotFound;
    public const string RefNumConflict = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.RefNumConflict;
    public const string TopUpAmountMismatch = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.TopUpAmountMismatch;
    public const string TopUpRequiresReview = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.TopUpRequiresReview;
    public const string GatewayResultConflict = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.GatewayResultConflict;
    public const string InvalidVerificationData = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.InvalidVerificationData;
    public const string TopUpConcurrentConfirmation = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.TopUpConcurrentConfirmation;
    public const string TopUpAlreadyConfirmed = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.TopUpAlreadyConfirmed;
    public const string ServiceUnauthorized = "SERVICE_UNAUTHORIZED";
    public const string ReviewConflict = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewConflict;
    public const string ReviewStateMismatch = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewStateMismatch;
    public const string ReviewRefundUnsupported = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewRefundUnsupported;
    public const string ReviewReasonInvalid = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewReasonInvalid;
    public const string ReviewOutcomeInvalid = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewOutcomeInvalid;
    public const string ReviewFinancialReferenceInvalid = ByeMoney.Application.Modules.Wallet.Constants.GatewayTopUpErrorCodes.ReviewFinancialReferenceInvalid;
}

