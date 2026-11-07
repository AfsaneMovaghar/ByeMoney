using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriodOrders;

/// <summary>سفارش‌های یک دورهٔ بسته‌شده با مقادیر Snapshot لحظهٔ بستن.</summary>
public sealed record GetOrderSettlementPeriodOrdersQuery(Guid PeriodId, int Page = 1, int PageSize = 20) :
    IRequest<PurchasePage<SettlementOrderResponse>>, ISettlementPagedQuery;
