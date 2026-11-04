namespace ByeMoney.API.Contracts.Purchases;

/// <summary>بدنهٔ لغو کل بخش فیزیکی سبد توسط کاربر؛ <c>OperationId</c> شناسهٔ یکتای همین عملیات لغو است.</summary>
public sealed record CancelMyPhysicalOrderApiRequest(Guid OperationId, string Reason);

/// <summary>بدنهٔ لغو قلم یا تعداد توسط ادمین؛ <c>OperationId</c> شناسهٔ یکتای همین عملیات لغو است.</summary>
public sealed record AdminCancelPhysicalPurchaseApiRequest(Guid OperationId, int Quantity, string Reason);

public sealed record PhysicalCancellationErrorResponse(string Code, string Error);
