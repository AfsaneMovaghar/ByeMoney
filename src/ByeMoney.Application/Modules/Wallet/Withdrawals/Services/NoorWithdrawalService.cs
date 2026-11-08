using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;

namespace ByeMoney.Application.Modules.Wallet.Withdrawals.Services;

/// <summary>
/// هماهنگی خروج نور توسط ادمین (D4، D09، E3):
/// ثبت خروج فقط نور عادی را با ثبت Ledger متوازن کم می‌کند؛ ثبت واریز اثر مالی ندارد؛
/// «پرداخت نشد» نور را با ثبت جبرانی مرتبط از سرویس واحد افزایش نور برمی‌گرداند (D05).
/// همهٔ تغییرها زیر قفل خریدار انجام می‌شوند تا با خرید هم‌زمان روی همان کیف پول سریال شوند.
/// </summary>
public sealed class NoorWithdrawalService(
    INoorWithdrawalStore store,
    IUserRepository users,
    ITopUpRequestRepository topUps,
    IWalletRepository wallets,
    IAccountRepository accounts,
    IUserWalletProvisioningService walletProvisioning,
    IWalletPostingService posting,
    INoorCreditService noorCredit,
    ISystemSettingRepository settings,
    IReceiptStorage receipts)
{
    private const int NoorDecimals = 4;

    public async Task<Result<NoorWithdrawalResponse>> CreateAsync(
        Guid adminId, CreateNoorWithdrawalCommand request, CancellationToken ct)
    {
        NoorWithdrawalRules.TryParseKind(request.Kind, out var kind);

        var user = await users.GetByExternalUserIdAsync(request.UserExternalId.Trim(), ct);
        if (user is null)
        {
            return Result<NoorWithdrawalResponse>.NotFound(
                NoorWithdrawalErrors.UserNotFound, NoorWithdrawalErrorCodes.UserNotFound);
        }

        TopUpRequestId? topUpId = null;
        if (kind == NoorWithdrawalKind.DuplicatePaymentRefund)
        {
            topUpId = new TopUpRequestId(request.RelatedTopUpId!.Value);
        }

        var amountNoor = await ResolveAmountNoorAsync(request.AmountRial, topUpId, user.Id, ct);
        if (amountNoor.IsFailure)
        {
            return Result<NoorWithdrawalResponse>.Failure(amountNoor.ErrorMessage!, amountNoor.ErrorCode);
        }

        var adminUserId = new UserId(adminId);
        var result = await store.ExecuteAsync(user.Id, async () =>
        {
            var existing = await store.FindByOperationIdAsync(request.OperationId, ct);
            if (existing is not null)
            {
                return IsSameCreate(existing, request, user.Id, kind, topUpId, adminUserId)
                    ? Result<NoorWithdrawalId>.Success(existing.Id)
                    : Result<NoorWithdrawalId>.Conflict(
                        NoorWithdrawalErrors.IdempotencyConflict, NoorWithdrawalErrorCodes.IdempotencyConflict);
            }

            if (topUpId.HasValue && await store.HasActiveRefundForTopUpAsync(topUpId.Value, ct))
            {
                return Result<NoorWithdrawalId>.Conflict(
                    NoorWithdrawalErrors.TopUpAlreadyRefunded, NoorWithdrawalErrorCodes.TopUpAlreadyRefunded);
            }

            return await DebitAsync(request, amountNoor.Value, user.Id, kind, topUpId, adminUserId, ct);
        }, ct);

        return await ToResponseAsync(result, ct);
    }

    public async Task<Result<NoorWithdrawalResponse>> MarkPaidAsync(
        Guid adminId, Guid withdrawalId, string paymentReference, string receiptId, CancellationToken ct)
    {
        var id = new NoorWithdrawalId(withdrawalId);
        var owner = await store.FindOwnerAsync(id, ct);
        if (owner is null)
        {
            return NotFound();
        }

        var reference = paymentReference.Trim();
        var result = await store.ExecuteAsync(owner.Value, async () =>
        {
            var withdrawal = await store.FindAsync(id, ct);
            if (withdrawal!.Status == NoorWithdrawalStatus.Paid)
            {
                // تکرار همان ثبت واریز (مثلاً پس از قطع شبکه) همان نتیجه را برمی‌گرداند.
                var sameRequest = withdrawal.PaymentReference == reference && withdrawal.ReceiptId == receiptId;
                return sameRequest ? Result<NoorWithdrawalId>.Success(id) : NotPending();
            }

            if (withdrawal.Status != NoorWithdrawalStatus.PendingPayment)
            {
                return NotPending();
            }

            withdrawal.MarkPaid(reference, receiptId, new UserId(adminId));
            return Result<NoorWithdrawalId>.Success(id);
        }, ct);

        return await ToResponseAsync(result, ct);
    }

    public async Task<Result<NoorWithdrawalResponse>> ReverseAsync(
        Guid adminId, Guid withdrawalId, string reason, CancellationToken ct)
    {
        var id = new NoorWithdrawalId(withdrawalId);
        var owner = await store.FindOwnerAsync(id, ct);
        if (owner is null)
        {
            return NotFound();
        }

        var result = await store.ExecuteAsync(owner.Value, async () =>
        {
            var withdrawal = await store.FindAsync(id, ct);
            if (withdrawal!.Status == NoorWithdrawalStatus.Reversed)
            {
                // جبران قبلاً انجام شده است؛ تکرار، برگشت دوم نمی‌سازد (T25).
                return Result<NoorWithdrawalId>.Success(id);
            }

            if (withdrawal.Status != NoorWithdrawalStatus.PendingPayment)
            {
                return NotPending();
            }

            var adminUserId = new UserId(adminId);
            var credit = await noorCredit.CreditAsync(new NoorCreditRequest(
                UserId: withdrawal.UserId,
                AmountNoor: withdrawal.AmountNoor,
                BalanceType: LedgerBalanceType.Regular,
                ReferenceType: LedgerReferenceType.WithdrawalReversal,
                ReferenceId: withdrawal.Id.ToString(),
                PerformedByUserId: adminUserId,
                Reason: reason), ct);

            withdrawal.MarkReversed(reason, credit.TransactionId, adminUserId);
            return Result<NoorWithdrawalId>.Success(id);
        }, ct);

        return await ToResponseAsync(result, ct);
    }

    public async Task<NoorWithdrawalReceiptFile?> OpenReceiptAsync(Guid withdrawalId, CancellationToken ct)
    {
        var receiptId = await store.GetReceiptIdAsync(new NoorWithdrawalId(withdrawalId), ct);
        if (receiptId is null)
        {
            return null;
        }

        var content = await receipts.OpenReadAsync(receiptId, ct);
        return content is null ? null : new NoorWithdrawalReceiptFile(content, receiptId);
    }

    /// <summary>فیشی که در عملیات ناموفق ذخیره شد و به هیچ خروجی وصل نشد، پاک می‌شود.</summary>
    public async Task DeleteUnreferencedReceiptAsync(string receiptId, CancellationToken ct)
    {
        if (!await store.IsReceiptReferencedAsync(receiptId, ct))
        {
            await receipts.DeleteAsync(receiptId, ct);
        }
    }

    /// <summary>
    /// نور کسرشده از مبلغ ریالی حساب می‌شود تا کاربر سود یا زیان نرخ نبیند (D09).
    /// صلاحدید ادمین با نرخ فعلی سیستم؛ استرداد پرداخت تکراری با نرخ ثبت‌شدهٔ خود شارژ.
    /// </summary>
    private async Task<Result<decimal>> ResolveAmountNoorAsync(
        decimal amountRial, TopUpRequestId? topUpId, UserId userId, CancellationToken ct)
    {
        if (topUpId.HasValue)
        {
            return await ResolveRefundAmountNoorAsync(amountRial, topUpId.Value, userId, ct);
        }

        var rate = await settings.GetRialToNoorConversionRateAsync(ct);
        return ConvertToNoor(amountRial, rate);
    }

    private async Task<Result<decimal>> ResolveRefundAmountNoorAsync(
        decimal amountRial, TopUpRequestId topUpId, UserId userId, CancellationToken ct)
    {
        // استرداد فقط برای شارژی است که نورش واقعاً به کیف پول همین کاربر رسیده است؛
        // پرداختی که به نور تبدیل نشده، از مسیر رسیدگی درگاه (ManuallyRefunded) بسته می‌شود.
        var topUp = await topUps.GetByIdAsync(topUpId, ct);
        if (topUp is null ||
            topUp.UserId != userId ||
            topUp.Status != TopUpStatus.Confirmed ||
            topUp.AmountRial is null ||
            topUp.RialPerNoorSnapshot is null)
        {
            return Result<decimal>.Failure(NoorWithdrawalErrors.TopUpInvalid, NoorWithdrawalErrorCodes.TopUpInvalid);
        }

        if (amountRial > topUp.AmountRial.Value)
        {
            return Result<decimal>.Failure(
                NoorWithdrawalErrors.AmountExceedsTopUp, NoorWithdrawalErrorCodes.AmountExceedsTopUp);
        }

        var converted = ConvertToNoor(amountRial, topUp.RialPerNoorSnapshot.Value);
        return converted.IsFailure
            ? converted
            : Result<decimal>.Success(Math.Min(converted.Value, topUp.AmountNoor));
    }

    private static Result<decimal> ConvertToNoor(decimal amountRial, decimal rialPerNoor)
    {
        if (rialPerNoor <= 0)
        {
            return Result<decimal>.Failure(NoorWithdrawalErrors.RateUnavailable, NoorWithdrawalErrorCodes.RateUnavailable);
        }

        var noor = decimal.Round(amountRial / rialPerNoor, NoorDecimals, MidpointRounding.ToEven);
        return noor > 0
            ? Result<decimal>.Success(noor)
            : Result<decimal>.Failure(NoorWithdrawalErrors.AmountTooSmall, NoorWithdrawalErrorCodes.AmountTooSmall);
    }

    private async Task<Result<NoorWithdrawalId>> DebitAsync(
        CreateNoorWithdrawalCommand request,
        decimal amountNoor,
        UserId userId,
        NoorWithdrawalKind kind,
        TopUpRequestId? topUpId,
        UserId adminUserId,
        CancellationToken ct)
    {
        var wallet = await wallets.GetByUserIdForUpdateAsync(userId, ct);
        if (wallet is null || wallet.WithdrawableBalance < amountNoor)
        {
            return Result<NoorWithdrawalId>.Failure(
                NoorWithdrawalErrors.InsufficientBalance, NoorWithdrawalErrorCodes.InsufficientBalance);
        }

        var account = await accounts.GetByUserIdAsync(userId, ct);
        var systemAccount = await walletProvisioning.GetOrCreateSystemAccountAsync(ct);
        var transactionId = Guid.NewGuid();

        var withdrawal = NoorWithdrawal.Create(
            request.OperationId,
            userId,
            kind,
            topUpId,
            amountNoor,
            request.AmountRial,
            request.BeneficiaryName,
            request.BeneficiaryCardNumber,
            request.BeneficiaryIban,
            request.Description,
            adminUserId,
            transactionId);

        // فقط نور عادی کم می‌شود؛ نور هدیه هرگز قابل خروج نیست (E3).
        await posting.PostDebitAsync(
            new ProvisionedUserWallet(account!, wallet),
            systemAccount,
            amountNoor,
            WalletDebitPurpose.WithdrawalOrTransfer,
            transactionId,
            LedgerReferenceType.Withdrawal,
            withdrawal.Id.ToString(),
            adminUserId,
            ct);

        if (!string.IsNullOrWhiteSpace(request.PaymentReference))
        {
            withdrawal.MarkPaid(request.PaymentReference, request.ReceiptId!, adminUserId);
        }

        store.Add(withdrawal);
        return Result<NoorWithdrawalId>.Success(withdrawal.Id);
    }

    private static bool IsSameCreate(
        NoorWithdrawal existing,
        CreateNoorWithdrawalCommand request,
        UserId userId,
        NoorWithdrawalKind kind,
        TopUpRequestId? topUpId,
        UserId adminUserId)
    {
        var sameCore = existing.UserId == userId &&
            existing.Kind == kind &&
            existing.RelatedTopUpId == topUpId &&
            existing.AmountRial == request.AmountRial &&
            existing.BeneficiaryName == request.BeneficiaryName.Trim() &&
            existing.BeneficiaryCardNumber == Normalize(request.BeneficiaryCardNumber) &&
            existing.BeneficiaryIban == Normalize(request.BeneficiaryIban) &&
            existing.Description == request.Description.Trim() &&
            existing.CreatedByUserId == adminUserId;

        if (!sameCore || string.IsNullOrWhiteSpace(request.PaymentReference))
        {
            // تکرار ثبت بدون واریز، حتی اگر واریز بعداً ثبت شده باشد، همان عملیات است.
            return sameCore;
        }

        return existing.PaymentReference == request.PaymentReference.Trim() &&
            existing.ReceiptId == request.ReceiptId;
    }

    private async Task<Result<NoorWithdrawalResponse>> ToResponseAsync(
        Result<NoorWithdrawalId> result, CancellationToken ct)
    {
        if (result.IsFailure)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => Result<NoorWithdrawalResponse>.NotFound(result.ErrorMessage, result.ErrorCode),
                ResultStatus.Conflict => Result<NoorWithdrawalResponse>.Conflict(result.ErrorMessage!, result.ErrorCode),
                _ => Result<NoorWithdrawalResponse>.Failure(result.ErrorMessage!, result.ErrorCode)
            };
        }

        var response = await store.GetResponseAsync(result.Value, ct);
        return Result<NoorWithdrawalResponse>.Success(response!);
    }

    private static Result<NoorWithdrawalResponse> NotFound() =>
        Result<NoorWithdrawalResponse>.NotFound(NoorWithdrawalErrors.NotFound, NoorWithdrawalErrorCodes.NotFound);

    private static Result<NoorWithdrawalId> NotPending() =>
        Result<NoorWithdrawalId>.Conflict(NoorWithdrawalErrors.NotPending, NoorWithdrawalErrorCodes.NotPending);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
