using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;

/// <summary>پیش‌نمایش دورهٔ جاری: سفارش‌های قابل تسویهٔ تسویه‌نشده، تعداد و جمع نور خالص.</summary>
public sealed record GetOrderSettlementPreviewQuery(int Page = 1, int PageSize = 20) :
    IRequest<OrderSettlementPreviewResponse>, ISettlementPagedQuery;
