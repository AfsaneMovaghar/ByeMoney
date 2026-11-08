using Microsoft.AspNetCore.Http;

namespace ByeMoney.API.Contracts.Wallet;

/// <summary>
/// ثبت خروج نور (multipart/form-data). اگر واریز پیش‌تر انجام شده، <c>PaymentReference</c> و <c>Receipt</c> با هم فرستاده می‌شوند.
/// </summary>
public sealed record CreateNoorWithdrawalApiRequest(
    Guid OperationId,
    string UserExternalId,
    string Kind,
    Guid? RelatedTopUpId,
    decimal AmountRial,
    string BeneficiaryName,
    string? BeneficiaryCardNumber,
    string? BeneficiaryIban,
    string Description,
    string? PaymentReference,
    IFormFile? Receipt);

/// <summary>ثبت واریز یک خروج در انتظار (multipart/form-data)؛ شناسهٔ پرداخت و فیش هر دو الزامی‌اند.</summary>
public sealed record MarkNoorWithdrawalPaidApiRequest(string PaymentReference, IFormFile? Receipt);

public sealed record ReverseNoorWithdrawalApiRequest(string Reason);

public sealed record NoorWithdrawalErrorResponse(string Code, string Error);
