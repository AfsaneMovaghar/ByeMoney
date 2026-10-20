

namespace ByeMoney.Domain.Modules.Purchases;

public enum CourseCartStatus
{
    Preparing,
    Ready,
    AwaitingTopUp,
    Paid,
    Failed,
    Expired
}
