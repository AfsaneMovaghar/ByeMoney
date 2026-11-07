namespace ByeMoney.API.Contracts.Purchases;

/// <summary>
/// بدنهٔ بستن دورهٔ تسویه. <c>PreviewToken</c> همان مقدار پیش‌نمایشی است که ادمین تأیید کرده است.
/// </summary>
public sealed record CloseOrderSettlementPeriodApiRequest(
    string? Title,
    string? Note,
    string PreviewToken);

public sealed record OrderSettlementErrorResponse(string Code, string Error);
