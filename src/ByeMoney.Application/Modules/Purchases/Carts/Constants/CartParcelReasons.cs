namespace ByeMoney.Application.Modules.Purchases.Carts.Constants;

public static class CartParcelReasons
{
    public const string MissingBox = "MISSING_BOX";

    public const string MissingWeight = "MISSING_WEIGHT";

    public static bool IsParcelGap(string? reason) =>
        reason is MissingBox or MissingWeight;
}
