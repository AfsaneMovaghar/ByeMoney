using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandValidator : AbstractValidator<CreateTopUpRequestCommand>
{
    public CreateTopUpRequestCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("شناسه کاربر الزامی است.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("مبلغ افزایش موجودی باید بزرگتر از صفر باشد.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("روش پرداخت نامعتبر است.");
    }
}

