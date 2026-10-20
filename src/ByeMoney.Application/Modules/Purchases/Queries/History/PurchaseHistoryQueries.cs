using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.History;

public sealed record GetMyCoursePurchasesQuery(int Page = 1, int PageSize = 20)
    : IRequest<PurchasePage<PurchaseListItem>>;

public sealed record GetMyCoursePurchaseReceiptQuery(Guid PurchaseId) : IRequest<PurchaseReceipt>;

public sealed record GetAdminCoursePurchaseReceiptQuery(Guid PurchaseId) : IRequest<PurchaseReceipt>;

public sealed record GetAdminCoursePurchasesQuery(PurchaseHistoryFilter Filter)
    : IRequest<PurchasePage<AdminPurchaseListItem>>;

public sealed class PurchaseHistoryFilterValidator : AbstractValidator<PurchaseHistoryFilter>
{
    public PurchaseHistoryFilterValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(x => x.Page).GreaterThan(0)
            .WithMessage(PurchaseHistoryErrors.InvalidPage)
            .Must((filter, page) => filter.PageSize is >= 1 and <= 100 && page <= int.MaxValue / filter.PageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(x => x.BuyerExternalUserId)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithMessage(PurchaseHistoryErrors.InvalidBuyer)
            .MaximumLength(100).WithMessage(PurchaseHistoryErrors.InvalidBuyer);
        RuleFor(x => x.ToUtc)
            .Must((filter, end) => !end.HasValue || !filter.FromUtc.HasValue || end > filter.FromUtc)
            .WithMessage(PurchaseHistoryErrors.InvalidDateRange);
        RuleFor(x => x.FinancialStatus)
            .Must(value => value is null || value == PurchaseFinancialStatus.Paid.ToString())
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(x => x.DeliveryStatus)
            .Must(value => value is null || value == PurchaseDeliveryStatus.Pending.ToString() ||
                value == PurchaseDeliveryStatus.Delivered.ToString())
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
    }
}

public sealed class GetMyCoursePurchasesQueryValidator : AbstractValidator<GetMyCoursePurchasesQuery>
{
    public GetMyCoursePurchasesQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => new PurchaseHistoryFilter(x.Page, x.PageSize))
            .SetValidator(new PurchaseHistoryFilterValidator());
    }
}

public sealed class GetAdminCoursePurchasesQueryValidator : AbstractValidator<GetAdminCoursePurchasesQuery>
{
    public GetAdminCoursePurchasesQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .SetValidator(new PurchaseHistoryFilterValidator());
    }
}

public sealed class GetMyCoursePurchaseReceiptQueryValidator : AbstractValidator<GetMyCoursePurchaseReceiptQuery>
{
    public GetMyCoursePurchaseReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PurchaseId).NotEmpty().WithMessage(PurchaseHistoryErrors.InvalidPurchaseId);
    }
}

public sealed class GetAdminCoursePurchaseReceiptQueryValidator : AbstractValidator<GetAdminCoursePurchaseReceiptQuery>
{
    public GetAdminCoursePurchaseReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PurchaseId).NotEmpty().WithMessage(PurchaseHistoryErrors.InvalidPurchaseId);
    }
}

public sealed class PurchaseHistoryQueryHandlers(
    IPurchaseHistoryReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCoursePurchasesQuery, PurchasePage<PurchaseListItem>>,
    IRequestHandler<GetMyCoursePurchaseReceiptQuery, PurchaseReceipt>,
    IRequestHandler<GetAdminCoursePurchaseReceiptQuery, PurchaseReceipt>,
    IRequestHandler<GetAdminCoursePurchasesQuery, PurchasePage<AdminPurchaseListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<PurchaseListItem>> Handle(GetMyCoursePurchasesQuery request, CancellationToken ct)
    {
        var page = await reader.ListAsync(BuyerId, new PurchaseHistoryFilter(request.Page, request.PageSize), ct);
        return new PurchasePage<PurchaseListItem>(
            page.Items.Select(PurchaseHistoryMapping.ToListItem).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PurchaseReceipt> Handle(GetMyCoursePurchaseReceiptQuery request, CancellationToken ct)
    {
        var data = await reader.GetReceiptAsync(request.PurchaseId, BuyerId, ct)
            ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        return PurchaseHistoryMapping.ToReceipt(data);
    }

    public async Task<PurchasePage<AdminPurchaseListItem>> Handle(GetAdminCoursePurchasesQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var page = await reader.ListAsync(null, request.Filter, ct);
        return new PurchasePage<AdminPurchaseListItem>(
            page.Items.Select(PurchaseHistoryMapping.ToAdminListItem).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PurchaseReceipt> Handle(GetAdminCoursePurchaseReceiptQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var data = await reader.GetAdminReceiptAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        return PurchaseHistoryMapping.ToReceipt(data);
    }
}
