

namespace ByeMoney.Domain.Modules.Purchases;

public enum CartStatus
{
    Preparing,
    Ready,
    AwaitingTopUp,
    Paid,
    Failed,
    Expired
}
