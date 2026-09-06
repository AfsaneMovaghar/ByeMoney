using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public class RejectTopUpCommandValidator : AbstractValidator<RejectTopUpCommand>
{
    public RejectTopUpCommandValidator()
    {
        RuleFor(x => x.TopUpRequestId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_IdRequired);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_RejectionReasonRequired);
    }
}

