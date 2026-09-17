namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;

public record InsufficientBalanceFailureState(
    decimal CurrentBalanceInNoor,
    decimal PriceInNoor,
    decimal ShortfallInNoor,
    decimal ShortfallInRial,
    string ErrorCode = "INSUFFICIENT_NOOR_BALANCE");

