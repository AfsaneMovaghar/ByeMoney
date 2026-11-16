using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;

/// <summary>
/// تأیید دریافت کالا توسط خریدار برای همهٔ اقلام «ارسال‌شده» همان سفارش.
/// <c>OrderId</c> همان قالب I11-T است (<c>cart:{id}</c> یا <c>purchase:{id}</c>).
/// </summary>
public sealed record ConfirmMyPhysicalDeliveryCommand(string OrderId) :
    IRequest<PhysicalDeliveryConfirmationResponse>;

public sealed record PhysicalDeliveryConfirmationResponse(
    string OrderId, int ConfirmedCount, IReadOnlyList<Guid> PurchaseIds);
