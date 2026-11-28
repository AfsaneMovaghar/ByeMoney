namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;

public sealed record ClosedParcelRefundResponse(
    Guid OperationId,
    Guid CartId,
    Guid PurchaseId,
    decimal RegularRefundInNoor,
    decimal GiftRefundInNoor,
    decimal TotalRefundInNoor,
    string Reason,
    DateTime CreatedAtUtc);
