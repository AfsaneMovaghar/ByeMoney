using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;

/// <summary>
/// ثبت خروج نور عادی کاربر توسط ادمین (D4). اگر شناسهٔ پرداخت و فیش با هم آمده باشند، واریز پیش‌تر انجام شده
/// و رکورد در همان تراکنش «پرداخت‌شده» ثبت می‌شود؛ وگرنه «در انتظار پرداخت» می‌ماند.
/// ادمین فقط مبلغ ریالی را می‌دهد؛ نور کسرشده را سرویس حساب می‌کند (D09).
/// </summary>
public sealed record CreateNoorWithdrawalCommand(
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
    string? ReceiptId) : IRequest<Result<NoorWithdrawalResponse>>;
