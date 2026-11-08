using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReverseNoorWithdrawal;

public sealed class ReverseNoorWithdrawalCommandValidator : AbstractValidator<ReverseNoorWithdrawalCommand>
{
    public ReverseNoorWithdrawalCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.WithdrawalId)
            .NotEmpty()
            .WithMessage(NoorWithdrawalErrors.InvalidWithdrawalId);

        RuleFor(x => x.Reason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidReversalReason)
            .MaximumLength(NoorWithdrawal.ReversalReasonMaxLength)
            .WithMessage(NoorWithdrawalErrors.InvalidReversalReason);
    }
}
