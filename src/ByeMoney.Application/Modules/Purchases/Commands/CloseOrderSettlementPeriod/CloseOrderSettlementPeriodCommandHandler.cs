using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Constants;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;

/// <summary>
/// فهرست قابل تسویه را لحظهٔ بستن دوباره محاسبه می‌کند و با پیش‌نمایش تأییدشده تطبیق می‌دهد.
/// یکتایی هر سفارش در دوره‌ها با قید دیتابیس تضمین می‌شود، نه با این بررسی. هیچ ثبت Ledger یا تغییر مانده‌ای ندارد.
/// </summary>
public sealed class CloseOrderSettlementPeriodCommandHandler(
    OrderSettlementCandidateService candidates,
    IOrderSettlementStore store,
    ICurrentUserService currentUser) :
    IRequestHandler<CloseOrderSettlementPeriodCommand, Result<OrderSettlementPeriodResponse>>
{
    public async Task<Result<OrderSettlementPeriodResponse>> Handle(
        CloseOrderSettlementPeriodCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var orders = await candidates.GetUnsettledAsync(ct);
        if (orders.Count == 0)
        {
            return Result<OrderSettlementPeriodResponse>.Conflict(
                OrderSettlementErrors.Empty, OrderSettlementErrorCodes.Empty);
        }

        if (OrderSettlementPreviewToken.Compute(orders) != request.PreviewToken)
        {
            return Result<OrderSettlementPeriodResponse>.Conflict(
                OrderSettlementErrors.PreviewChanged, OrderSettlementErrorCodes.PreviewChanged);
        }

        var items = orders
            .Select(order => OrderSettlementItem.Create(
                order.OrderId,
                order.SourceType,
                order.BuyerExternalUserId,
                order.OrderCreatedAtUtc,
                new PaymentShares(order.RegularNetAmountInNoor, order.GiftNetAmountInNoor)))
            .ToArray();
        var period = OrderSettlementPeriod.Close(new UserId(adminId), request.Title, request.Note, items);

        if (!await store.TryAddAsync(period, ct))
        {
            return Result<OrderSettlementPeriodResponse>.Conflict(
                OrderSettlementErrors.AlreadySettled, OrderSettlementErrorCodes.AlreadySettled);
        }

        var response = await store.FindPeriodAsync(period.Id.Value, ct)
            ?? throw new InvalidOperationException("A committed settlement period must be readable.");
        return Result<OrderSettlementPeriodResponse>.Success(response);
    }
}
