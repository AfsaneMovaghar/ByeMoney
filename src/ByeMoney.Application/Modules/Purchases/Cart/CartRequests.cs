using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using FluentValidation;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Cart;

public sealed record PrepareCourseCartCommand(
    Guid CartId,
    string[] ExternalCourseIds,
    string? CouponCode) : IRequest<Result<CartResponse>>;

public sealed record PurchaseCourseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;

public sealed record CreateCourseCartTopUpCommand(Guid CartId) : IRequest<Result<CreateTopUpRequestResponse>>;

public sealed record GetCourseCartQuery(Guid CartId) : IRequest<Result<CartResponse>>;

public sealed record CancelCourseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;

public sealed class CancelCourseCartValidator : AbstractValidator<CancelCourseCartCommand>
{
    public CancelCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}

public sealed class PrepareCourseCartValidator : AbstractValidator<PrepareCourseCartCommand>
{
    public PrepareCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.ExternalCourseIds)
            .NotNull()
            .WithMessage(CartErrors.Invalid)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .Must(ids => ids.Length <= 100 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length)
            .WithMessage(CartErrors.Invalid);

        RuleForEach(x => x.ExternalCourseIds)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(id => id == id.Trim())
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.CouponCode)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(code => code is null || !string.IsNullOrWhiteSpace(code))
            .WithMessage(CartErrors.Invalid);
    }
}

public sealed class PurchaseCourseCartValidator : AbstractValidator<PurchaseCourseCartCommand>
{
    public PurchaseCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}

public sealed class CreateCourseCartTopUpValidator : AbstractValidator<CreateCourseCartTopUpCommand>
{
    public CreateCourseCartTopUpValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}

public sealed class GetCourseCartValidator : AbstractValidator<GetCourseCartQuery>
{
    public GetCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}

public sealed class CartRequestHandlers(CourseCartService service, ICurrentUserService currentUser) :
    IRequestHandler<PrepareCourseCartCommand, Result<CartResponse>>,
    IRequestHandler<GetCourseCartQuery, Result<CartResponse>>,
    IRequestHandler<PurchaseCourseCartCommand, Result<CartResponse>>,
    IRequestHandler<CreateCourseCartTopUpCommand, Result<CreateTopUpRequestResponse>>,
    IRequestHandler<CancelCourseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(CancelCourseCartCommand request, CancellationToken ct) =>
        service.CancelAsync(Buyer, request.CartId, ct);

    public Task<Result<CartResponse>> Handle(PrepareCourseCartCommand request, CancellationToken ct) =>
        service.PrepareAsync(Buyer, request.CartId, request.ExternalCourseIds, request.CouponCode, ct);

    public Task<Result<CartResponse>> Handle(GetCourseCartQuery request, CancellationToken ct) =>
        service.GetAsync(Buyer, request.CartId, ct);

    public Task<Result<CartResponse>> Handle(PurchaseCourseCartCommand request, CancellationToken ct) =>
        service.PurchaseAsync(Buyer, request.CartId, ct);

    public Task<Result<CreateTopUpRequestResponse>> Handle(CreateCourseCartTopUpCommand request, CancellationToken ct) =>
        service.TopUpAsync(Buyer, request.CartId, ct);
}
