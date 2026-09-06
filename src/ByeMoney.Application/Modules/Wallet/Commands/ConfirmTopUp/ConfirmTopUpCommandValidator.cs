using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandValidator : AbstractValidator<ConfirmTopUpCommand>
{
    public ConfirmTopUpCommandValidator()
    {
        RuleFor(x => x.TopUpRequestId.Value)
            .NotEmpty()
            .WithMessage("شناسه درخواست افزایش موجودی الزامی است.");

        RuleFor(x => x.ExternalTransactionId)
            .NotEmpty()
            .WithMessage("شناسه تراکنش خارجی الزامی است.");

        RuleFor(x => x.ConfirmedAmount)
            .GreaterThan(0)
            .WithMessage("مبلغ تأیید شده باید بزرگتر از صفر باشد.");
    }
}

