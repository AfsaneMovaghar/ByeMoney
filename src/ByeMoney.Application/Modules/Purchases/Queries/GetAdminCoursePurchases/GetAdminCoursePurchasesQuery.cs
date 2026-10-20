using ByeMoney.Application.Modules.Purchases.History.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchases;

public sealed record GetAdminCoursePurchasesQuery(PurchaseHistoryFilter Filter)
    : IRequest<PurchasePage<AdminPurchaseListItem>>;
