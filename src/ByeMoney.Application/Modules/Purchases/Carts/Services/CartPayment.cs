using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.Application.Modules.Purchases.Carts.Services;

public static class CartPayment
{
    public static CartPaymentRecords Apply(
        Cart cart,
        ProvisionedUserWallet wallet,
        Account? system,
        decimal? referenceRate = null)
    {
        if (cart.Status is not (CartStatus.Ready or CartStatus.AwaitingTopUp))
        {
            throw new InvalidOperationException();
        }

        if (cart.Total < 0 ||
            cart.Items.Any(x => x.AllocatedAmountInNoor < 0 ||
                decimal.Truncate(x.AllocatedAmountInNoor) != x.AllocatedAmountInNoor ||
                x.AllocatedAmountInNoor > x.LineTotalInNoor) ||
            cart.Items.Sum(x => x.AllocatedAmountInNoor) + cart.ShippingAmountInNoor != cart.Total)
        {
            throw new ArgumentException(nameof(cart));
        }

        var items = cart.Items;
        var partAmounts = items.Select(x => x.AllocatedAmountInNoor)
            .Append(cart.ShippingAmountInNoor)
            .ToArray();
        var giftShares = new decimal[partAmounts.Length];
        var ledgerEntries = new List<LedgerEntry>();

        if (cart.Total > 0)
        {
            ArgumentNullException.ThrowIfNull(system);
            var debitResult = wallet.Wallet.Debit(cart.Total, WalletDebitPurpose.Purchase);
            giftShares = PaymentShareAllocator.SplitGift(debitResult.GiftDebited, partAmounts);
        }

        for (var i = 0; i < partAmounts.Length; i++)
        {
            if (partAmounts[i] <= 0)
            {
                continue;
            }

            var referenceId = i < items.Count
                ? CartPurchaseIds.For(cart.Id, items[i].ExternalCourseId).Value.ToString()
                : cart.Id.ToString();
            AddLedgerPair(ledgerEntries, wallet, system!, cart.Id, referenceId, giftShares[i], LedgerBalanceType.Gift);
            AddLedgerPair(
                ledgerEntries, wallet, system!, cart.Id, referenceId, partAmounts[i] - giftShares[i], LedgerBalanceType.Regular);
        }

        var purchases = new List<CoursePurchase>();
        var physicalPurchases = new List<PhysicalProductPurchase>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var amount = item.AllocatedAmountInNoor;
            var shares = new PaymentShares(amount - giftShares[i], giftShares[i]);
            var purchaseIdGuid = CartPurchaseIds.For(cart.Id, item.ExternalCourseId).Value;

            if (item.ItemType == ProductItemType.Product)
            {
                var physicalPurchase = PhysicalProductPurchase.Create(
                    new PhysicalProductPurchaseId(purchaseIdGuid),
                    cart.Id,
                    cart.BuyerId,
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

                physicalPurchase.RecordPaymentShares(shares);
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
                    cart.BuyerId,
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

                purchase.RecordPaymentShares(shares);
                purchases.Add(purchase);
            }
        }

        cart.MarkPaid();
        return new CartPaymentRecords(purchases, physicalPurchases, ledgerEntries);
    }

    private static void AddLedgerPair(
        List<LedgerEntry> entries,
        ProvisionedUserWallet wallet,
        Account system,
        Guid transactionId,
        string referenceId,
        decimal amount,
        LedgerBalanceType balanceType)
    {
        if (amount <= 0)
        {
            return;
        }

        entries.Add(LedgerEntry.Create(
            wallet.Account.Id, -amount, transactionId, LedgerReferenceType.Purchase, referenceId, balanceType: balanceType));
        entries.Add(LedgerEntry.Create(
            system.Id, amount, transactionId, LedgerReferenceType.Purchase, referenceId, balanceType: balanceType));
    }
}
