using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawalReceipt;

public sealed class GetNoorWithdrawalReceiptQueryValidator : AbstractValidator<GetNoorWithdrawalReceiptQuery>
{
    public GetNoorWithdrawalReceiptQueryValidator()
    {
        RuleFor(x => x.WithdrawalId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(NoorWithdrawalErrors.InvalidWithdrawalId);
    }
}
