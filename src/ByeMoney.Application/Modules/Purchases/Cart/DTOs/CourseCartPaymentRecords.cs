using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CourseCartPaymentRecords(
    IReadOnlyList<CoursePurchase> Purchases,
    IReadOnlyList<PhysicalProductPurchase> PhysicalPurchases,
    IReadOnlyList<LedgerEntry> LedgerEntries);
