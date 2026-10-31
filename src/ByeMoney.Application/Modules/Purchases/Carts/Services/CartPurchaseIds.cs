using System.Security.Cryptography;
using System.Text;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Carts.Services;

public static class CartPurchaseIds
{
    public static CoursePurchaseId For(Guid cartId, string courseId) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"course-cart:{cartId:D}:{courseId}"))[..16]));
}
