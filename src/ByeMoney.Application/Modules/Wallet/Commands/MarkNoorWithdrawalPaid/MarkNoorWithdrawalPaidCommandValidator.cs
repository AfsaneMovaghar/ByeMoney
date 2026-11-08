using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.MarkNoorWithdrawalPaid;

public sealed class MarkNoorWithdrawalPaidCommandValidator : AbstractValidator<MarkNoorWithdrawalPaidCommand>
{
    public MarkNoorWithdrawalPaidCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.WithdrawalId)
            .NotEmpty()
            .WithMessage(NoorWithdrawalErrors.InvalidWithdrawalId);

        RuleFor(x => x.PaymentReference)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidPaymentReference)
            .MaximumLength(NoorWithdrawal.PaymentReferenceMaxLength)
            .WithMessage(NoorWithdrawalErrors.InvalidPaymentReference);

        RuleFor(x => x.ReceiptId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.ReceiptRequired)
            .MaximumLength(NoorWithdrawal.ReceiptIdMaxLength)
            .WithMessage(NoorWithdrawalErrors.ReceiptRequired);
    }
}
