namespace ByeMoney.Application.Modules.Purchases.Orders.Constants;

public enum CourseOrderFinancialStatus
{
    Preparing,
    AwaitingPayment,
    Processing,
    Paid,
    Failed,
    Expired,
    RequiresReview
}
