using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolvePhysicalRestock;

public sealed class ResolvePhysicalRestockCommandValidator : AbstractValidator<ResolvePhysicalRestockCommand>
{
    public ResolvePhysicalRestockCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.OperationId)
            .NotEmpty()
            .WithMessage(PhysicalCancellationErrors.InvalidOperationId);
    }
}
