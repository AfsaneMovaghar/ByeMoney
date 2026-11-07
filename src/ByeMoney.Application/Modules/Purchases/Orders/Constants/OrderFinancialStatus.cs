namespace ByeMoney.Application.Modules.Purchases.Orders.Constants;

public enum OrderFinancialStatus
{
    Preparing,
    AwaitingPayment,
    Processing,
    Paid,
    Failed,
    Expired,
    RequiresReview
}
