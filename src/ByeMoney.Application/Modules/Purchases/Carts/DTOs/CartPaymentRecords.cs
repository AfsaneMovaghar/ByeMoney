using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Purchases.Carts.DTOs;

public sealed record CartPaymentRecords(
    IReadOnlyList<CoursePurchase> Purchases,
    IReadOnlyList<PhysicalProductPurchase> PhysicalPurchases,
    IReadOnlyList<LedgerEntry> LedgerEntries);

