using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Purchases.Cart.Services;

public static class CourseCartPayment
{
    public static CourseCartPaymentRecords Apply(
        CourseCart cart,
        ProvisionedUserWallet wallet,
        Account? system,
        decimal? referenceRate = null)
    {
        if (cart.Status is not (CourseCartStatus.Ready or CourseCartStatus.AwaitingTopUp))
        {
            throw new InvalidOperationException();
        }

        var allocations = cart.PurchaseAllocations.ToDictionary(x => x.ExternalCourseId);
        if (cart.Total < 0 || allocations.Count != cart.Items.Count ||
            allocations.Values.Any(x => x.AmountInNoor < 0 || decimal.Truncate(x.AmountInNoor) != x.AmountInNoor) ||
            cart.Items.Any(x => !allocations.ContainsKey(x.ExternalCourseId) || allocations[x.ExternalCourseId].AmountInNoor > x.PriceInNoor) ||
            allocations.Values.Sum(x => x.AmountInNoor) != cart.Total)
        {
            throw new ArgumentException(nameof(cart));
        }

        if (cart.Total > 0)
        {
            ArgumentNullException.ThrowIfNull(system);
            wallet.Wallet.ApplyDebit(cart.Total);
        }

        var purchases = new List<CoursePurchase>();
        var ledgerEntries = new List<LedgerEntry>();
        foreach (var item in cart.Items)
        {
            var amount = allocations[item.ExternalCourseId].AmountInNoor;
            var snapshot = ProductSnapshot.Create(
                item.ExternalCourseId,
                item.Title,
                amount,
                isFree: amount == 0,
                referenceRialPerNoorAtPurchaseTime: referenceRate);
            var purchase = CoursePurchase.Create(
                new UserId(cart.BuyerId),
                snapshot,
                amount == 0,
                purchaseId: CartPurchaseIds.For(cart.Id, item.ExternalCourseId));

            purchase.AttachToCart(cart.Id);
            purchase.MarkDebited(cart.Id);
            purchases.Add(purchase);

            if (amount == 0)
            {
                continue;
            }

            ledgerEntries.Add(LedgerEntry.Create(
                wallet.Account.Id, -amount, cart.Id, LedgerReferenceType.Purchase, purchase.Id.ToString()));
            ledgerEntries.Add(LedgerEntry.Create(
                system!.Id, amount, cart.Id, LedgerReferenceType.Purchase, purchase.Id.ToString()));
        }

        cart.MarkPaid();
        return new CourseCartPaymentRecords(purchases, ledgerEntries);
    }
}
