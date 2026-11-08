using System.Resources;

namespace ByeMoney.Application.Resources;

public static class NoorWithdrawalErrors
{
    private static readonly ResourceManager Resources = new(
        "ByeMoney.Application.Resources.NoorWithdrawalErrors", typeof(NoorWithdrawalErrors).Assembly);

    public static string NotFound => Get(nameof(NotFound));
    public static string UserNotFound => Get(nameof(UserNotFound));
    public static string InsufficientBalance => Get(nameof(InsufficientBalance));
    public static string TopUpInvalid => Get(nameof(TopUpInvalid));
    public static string AmountExceedsTopUp => Get(nameof(AmountExceedsTopUp));
    public static string TopUpAlreadyRefunded => Get(nameof(TopUpAlreadyRefunded));
    public static string NotPending => Get(nameof(NotPending));
    public static string IdempotencyConflict => Get(nameof(IdempotencyConflict));
    public static string Conflict => Get(nameof(Conflict));
    public static string InvalidOperationId => Get(nameof(InvalidOperationId));
    public static string InvalidWithdrawalId => Get(nameof(InvalidWithdrawalId));
    public static string InvalidUser => Get(nameof(InvalidUser));
    public static string InvalidKind => Get(nameof(InvalidKind));
    public static string TopUpRequired => Get(nameof(TopUpRequired));
    public static string TopUpNotAllowed => Get(nameof(TopUpNotAllowed));
    public static string RateUnavailable => Get(nameof(RateUnavailable));
    public static string AmountTooSmall => Get(nameof(AmountTooSmall));
    public static string InvalidAmountRial => Get(nameof(InvalidAmountRial));
    public static string InvalidBeneficiaryName => Get(nameof(InvalidBeneficiaryName));
    public static string InvalidCardNumber => Get(nameof(InvalidCardNumber));
    public static string InvalidIban => Get(nameof(InvalidIban));
    public static string BeneficiaryAccountRequired => Get(nameof(BeneficiaryAccountRequired));
    public static string InvalidDescription => Get(nameof(InvalidDescription));
    public static string InvalidPaymentReference => Get(nameof(InvalidPaymentReference));
    public static string ReceiptRequired => Get(nameof(ReceiptRequired));
    public static string PaymentWithoutReference => Get(nameof(PaymentWithoutReference));
    public static string InvalidReversalReason => Get(nameof(InvalidReversalReason));
    public static string InvalidStatus => Get(nameof(InvalidStatus));
    public static string InvalidLimit => Get(nameof(InvalidLimit));
    public static string InvalidReceipt => Get(nameof(InvalidReceipt));

    private static string Get(string name) => Resources.GetString(name)
        ?? throw new MissingManifestResourceException(name);
}
