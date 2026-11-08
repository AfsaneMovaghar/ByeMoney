using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawals;

public sealed class GetNoorWithdrawalsQueryValidator : AbstractValidator<GetNoorWithdrawalsQuery>
{
    public GetNoorWithdrawalsQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Status)
            .Must(status => NoorWithdrawalRules.TryParseStatus(status, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage(NoorWithdrawalErrors.InvalidStatus);

        RuleFor(x => x.UserExternalId)
            .MaximumLength(100)
            .WithMessage(NoorWithdrawalErrors.InvalidUser);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, GetNoorWithdrawalsQuery.MaxLimit)
            .WithMessage(NoorWithdrawalErrors.InvalidLimit);
    }
}
