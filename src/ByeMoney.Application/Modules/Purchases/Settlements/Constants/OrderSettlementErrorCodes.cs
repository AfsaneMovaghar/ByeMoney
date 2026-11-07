namespace ByeMoney.Application.Modules.Purchases.Settlements.Constants;

/// <summary>کدهای خطای ساختاریافتهٔ بستن دورهٔ تسویه؛ HTTP آن‌ها در کنترلر نگاشت می‌شود.</summary>
public static class OrderSettlementErrorCodes
{
    public const string Empty = "SETTLEMENT_EMPTY";
    public const string PreviewChanged = "SETTLEMENT_PREVIEW_CHANGED";
    public const string AlreadySettled = "SETTLEMENT_ALREADY_SETTLED";
}
