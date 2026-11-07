using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.RetryPhysicalRestock;

public sealed class RetryPhysicalRestockCommandValidator : AbstractValidator<RetryPhysicalRestockCommand>
{
    public RetryPhysicalRestockCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.OperationId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidOperationId);
    }
}
