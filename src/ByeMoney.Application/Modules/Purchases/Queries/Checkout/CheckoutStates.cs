namespace ByeMoney.Application.Modules.Purchases.Queries.Checkout;

public static class CheckoutStates
{
    public const string NotPurchased = "NotPurchased";
    public const string Completed = "Completed";
    public const string Activating = "Activating";
    public const string DeliveryPending = "DeliveryPending";
    public const string PurchaseIncomplete = "PurchaseIncomplete";
}
