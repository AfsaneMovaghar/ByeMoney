using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed class GetBatchWalletBalancesQueryValidator : AbstractValidator<GetBatchWalletBalancesQuery>
{
    public GetBatchWalletBalancesQueryValidator()
    {
        RuleFor(x => x.UserIds).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ApplicationErrors.Wallet_BatchUserIdsRequired)
            .Must(ids => ids.Count is > 0 and <= 500)
            .WithMessage(ApplicationErrors.Wallet_BatchUserIdsCountInvalid);

        RuleForEach(x => x.UserIds).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.Wallet_BatchUserIdInvalid)
            .MaximumLength(100).WithMessage(ApplicationErrors.Wallet_BatchUserIdInvalid);
    }
}
