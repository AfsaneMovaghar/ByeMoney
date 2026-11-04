namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;

/// <summary>
/// درخواست برگشت موجودی استرپی پس از لغو کالای پرداخت‌شده (I09-PC).
/// <paramref name="StrapiCartId"/> همان شناسهٔ رزرو سبد است که commit با آن انجام شده است.
/// </summary>
public sealed record PhysicalRestockRequest(
    Guid StrapiCartId,
    Guid OperationId,
    IReadOnlyList<PhysicalRestockItem> Items);

public sealed record PhysicalRestockItem(Guid PurchaseId, int Quantity);
