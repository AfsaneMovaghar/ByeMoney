using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public class RejectTopUpCommandValidator : AbstractValidator<RejectTopUpCommand>
{
    public RejectTopUpCommandValidator()
    {
        RuleFor(x => x.TopUpRequestId)
            .NotEmpty()
            .WithMessage("شناسه درخواست افزایش موجودی الزامی است.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("دلیل رد درخواست الزامی است.");
    }
}

