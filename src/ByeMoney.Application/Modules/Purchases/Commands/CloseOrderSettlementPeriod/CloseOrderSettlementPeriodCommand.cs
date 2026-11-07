using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;

/// <summary>
/// بستن دورهٔ تسویه (D10، فقط تگ گزارشی و بدون Ledger). <c>PreviewToken</c> همان مقدار پیش‌نمایشی است
/// که ادمین تأیید کرده؛ اگر فهرست در این فاصله تغییر کرده باشد، دوره بسته نمی‌شود.
/// </summary>
public sealed record CloseOrderSettlementPeriodCommand(
    string? Title,
    string? Note,
    string PreviewToken) : IRequest<Result<OrderSettlementPeriodResponse>>;
