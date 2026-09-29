using System.Security.Cryptography;
using System.Text;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Purchases.EventHandlers;

internal static class TopUpPurchaseId
{
    public static CoursePurchaseId For(TopUpRequestId topUpId, string externalItemId)
    {
        var input = Encoding.UTF8.GetBytes($"{topUpId.Value:N}:{externalItemId}");
        var hash = SHA256.HashData(input);
        return new CoursePurchaseId(new Guid(hash.AsSpan(0, 16)));
    }
}
