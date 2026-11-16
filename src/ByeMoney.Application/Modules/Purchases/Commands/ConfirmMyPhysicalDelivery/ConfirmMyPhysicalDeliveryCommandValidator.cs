using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;

public sealed class ConfirmMyPhysicalDeliveryCommandValidator : AbstractValidator<ConfirmMyPhysicalDeliveryCommand>
{
    public ConfirmMyPhysicalDeliveryCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .Must(OrderIdFormat.IsValid)
            .WithMessage(OrderErrors.InvalidOrderId);
    }
}
