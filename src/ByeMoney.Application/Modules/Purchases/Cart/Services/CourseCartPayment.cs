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

        var allocations = cart.PurchaseAllocations.ToDictionary(x => (x.ExternalCourseId, x.ItemType));
        if (cart.Total < 0 || allocations.Count != cart.Items.Count ||
            allocations.Values.Any(x => x.AmountInNoor < 0 || decimal.Truncate(x.AmountInNoor) != x.AmountInNoor) ||
            cart.Items.Any(x => !allocations.ContainsKey((x.ExternalCourseId, x.ItemType)) || allocations[(x.ExternalCourseId, x.ItemType)].AmountInNoor > x.LineTotalInNoor) ||
            allocations.Values.Sum(x => x.AmountInNoor) + cart.ShippingAmountInNoor != cart.Total)
        {
            throw new ArgumentException(nameof(cart));
        }

        if (cart.Total > 0)
        {
            ArgumentNullException.ThrowIfNull(system);
            wallet.Wallet.ApplyDebit(cart.Total);
        }

        var purchases = new List<CoursePurchase>();
        var physicalPurchases = new List<PhysicalProductPurchase>();
        var ledgerEntries = new List<LedgerEntry>();

        foreach (var item in cart.Items)
        {
            var amount = allocations[(item.ExternalCourseId, item.ItemType)].AmountInNoor;
            var purchaseIdGuid = CartPurchaseIds.For(cart.Id, item.ExternalCourseId).Value;

            if (item.ItemType == ProductItemType.Product)
            {
                var physicalPurchase = PhysicalProductPurchase.Create(
                    new PhysicalProductPurchaseId(purchaseIdGuid),
                    cart.Id,
                    new UserId(cart.BuyerId),
                    item.ExternalCourseId,
                    item.Title,
                    item.Quantity,
                    item.PriceInNoor,
                    amount);

                if (amount == 0)
                {
                    physicalPurchase.MarkFree(cart.Id);
                }
                else
                {
                    physicalPurchase.MarkDebited(cart.Id);
                }

                physicalPurchases.Add(physicalPurchase);
            }
            else
            {
                var snapshot = ProductSnapshot.Create(
                    item.ExternalCourseId,
                    item.Title,
                    amount,
                    isFree: amount == 0,
                    referenceRialPerNoorAtPurchaseTime: referenceRate,
                    itemType: item.ItemType,
                    externalParentId: item.ParentExternalId);

                var purchase = CoursePurchase.Create(
                    new UserId(cart.BuyerId),
                    snapshot,
                    amount == 0,
                    purchaseId: new CoursePurchaseId(purchaseIdGuid));

                purchase.AttachToCart(cart.Id);
                if (purchase.IsFree)
                {
                    purchase.MarkFree(cart.Id);
                }
                else
                {
                    purchase.MarkDebited(cart.Id);
                }

                purchases.Add(purchase);
            }
        }

        if (cart.Total > 0)
        {
            ledgerEntries.Add(LedgerEntry.Create(
                wallet.Account.Id, -cart.Total, cart.Id, LedgerReferenceType.Purchase, cart.Id.ToString()));
            ledgerEntries.Add(LedgerEntry.Create(
                system!.Id, cart.Total, cart.Id, LedgerReferenceType.Purchase, cart.Id.ToString()));
        }

        cart.MarkPaid();
        return new CourseCartPaymentRecords(purchases, physicalPurchases, ledgerEntries);
    }
}

