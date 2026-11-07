using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriods;

/// <summary>آرشیو دوره‌های تسویهٔ بسته‌شده، جدیدترین اول.</summary>
public sealed record GetOrderSettlementPeriodsQuery(int Page = 1, int PageSize = 20) :
    IRequest<PurchasePage<OrderSettlementPeriodResponse>>, ISettlementPagedQuery;
