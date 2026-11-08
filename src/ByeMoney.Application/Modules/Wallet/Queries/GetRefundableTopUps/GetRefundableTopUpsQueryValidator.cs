using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetRefundableTopUps;

public sealed class GetRefundableTopUpsQueryValidator : AbstractValidator<GetRefundableTopUpsQuery>
{
    public GetRefundableTopUpsQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.UserExternalId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(NoorWithdrawalErrors.InvalidUser)
            .MaximumLength(100)
            .WithMessage(NoorWithdrawalErrors.InvalidUser);
    }
}
