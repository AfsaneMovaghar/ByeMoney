using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandValidator : AbstractValidator<ConfirmTopUpCommand>
{
    public ConfirmTopUpCommandValidator()
    {
        RuleFor(x => x.TopUpRequestId)
            .NotEmpty()
            .WithMessage("شناسه درخواست افزایش موجودی الزامی است.");
    }
}

