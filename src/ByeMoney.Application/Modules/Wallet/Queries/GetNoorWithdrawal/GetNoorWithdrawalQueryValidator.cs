using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawal;

public sealed class GetNoorWithdrawalQueryValidator : AbstractValidator<GetNoorWithdrawalQuery>
{
    public GetNoorWithdrawalQueryValidator()
    {
        RuleFor(x => x.WithdrawalId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(NoorWithdrawalErrors.InvalidWithdrawalId);
    }
}
