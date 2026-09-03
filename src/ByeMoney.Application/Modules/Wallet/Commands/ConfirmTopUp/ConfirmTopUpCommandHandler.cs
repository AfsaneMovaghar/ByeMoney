using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using MediatR;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandHandler : IRequestHandler<ConfirmTopUpCommand, bool>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmTopUpCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IAccountRepository accountRepository,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        IUnitOfWork unitOfWork)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _accountRepository = accountRepository;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ConfirmTopUpCommand request, CancellationToken cancellationToken)
    {
        var topUpId = new TopUpRequestId(request.TopUpRequestId);
        var topUp = await _topUpRequestRepository.GetByIdAsync(topUpId, cancellationToken);

        if (topUp is null)
            throw new NotFoundException(nameof(TopUpRequest), request.TopUpRequestId);

        // 1. تغییر وضعیت دامنه به Confirmed
        topUp.Confirm(request.ExternalTransactionId);

        // 2. یافتن یا ایجاد حساب کاربری (UserAccount)
        var userAccount = await _accountRepository.GetByUserIdAsync(topUp.UserId, cancellationToken);
        if (userAccount is null)
        {
            userAccount = Account.CreateUserAccount(topUp.UserId);
            await _accountRepository.AddAsync(userAccount, cancellationToken);
        }

        // 3. یافتن یا ایجاد حساب سیستم (SystemAccount) جهت تراز صفر دفتر کل
        var systemAccount = await _accountRepository.GetSystemAccountAsync(cancellationToken);
        if (systemAccount is null)
        {
            systemAccount = Account.CreateSystemAccount();
            await _accountRepository.AddAsync(systemAccount, cancellationToken);
        }

        // 4. ثبت دو ردیف متقارن در دفتر کل (Zero-net Double-entry ledger entries)
        var transactionId = Guid.NewGuid();
        var userLedgerEntry = LedgerEntry.Create(
            userAccount.Id,
            topUp.Amount, // بستانکار (+Amount)
            transactionId,
            topUp.Id.ToString());

        var systemLedgerEntry = LedgerEntry.Create(
            systemAccount.Id,
            -topUp.Amount, // بدهکار (-Amount)
            transactionId,
            topUp.Id.ToString());

        await _ledgerRepository.AddAsync(userLedgerEntry, cancellationToken);
        await _ledgerRepository.AddAsync(systemLedgerEntry, cancellationToken);

        // 5. به‌روزرسانی اسنپ‌شات کیف پول کاربر
        var wallet = await _walletRepository.GetByAccountIdAsync(userAccount.Id, cancellationToken);
        if (wallet is null)
        {
            wallet = WalletEntity.Create(userAccount.Id, topUp.UserId);
            await _walletRepository.AddAsync(wallet, cancellationToken);
        }

        wallet.ApplyCredit(topUp.Amount);
        _walletRepository.Update(wallet);
        _topUpRequestRepository.Update(topUp);

        // 6. ذخیره اتمیک همه تغییرات
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

