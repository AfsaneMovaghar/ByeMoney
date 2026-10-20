using ByeMoney.Application.Modules.Purchases.History.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchases;

public sealed record GetMyCoursePurchasesQuery(int Page = 1, int PageSize = 20)
    : IRequest<PurchasePage<PurchaseListItem>>;
