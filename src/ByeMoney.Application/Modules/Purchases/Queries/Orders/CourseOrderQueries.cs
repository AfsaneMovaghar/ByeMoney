using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Queries.History;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.Orders;

public sealed record GetMyCourseOrdersQuery(CourseOrderFilter Filter)
    : IRequest<PurchasePage<CourseOrderListItem>>;
public sealed record GetMyCourseOrderQuery(string OrderId) : IRequest<CourseOrderDetails>;
public sealed record GetAdminCourseOrdersQuery(AdminCourseOrderFilter Filter)
    : IRequest<PurchasePage<AdminCourseOrderListItem>>;

public sealed class CourseOrderFilterValidator : AbstractValidator<CourseOrderFilter>
{
    public CourseOrderFilterValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(filter => filter.PageSize).InclusiveBetween(1, 100).WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(filter => filter.Page).GreaterThan(0).WithMessage(PurchaseHistoryErrors.InvalidPage)
            .Must((filter, page) => filter.PageSize is >= 1 and <= 100 && page <= int.MaxValue / filter.PageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(filter => filter.ToUtc)
            .Must((filter, end) => !end.HasValue || !filter.FromUtc.HasValue || end > filter.FromUtc)
            .WithMessage(PurchaseHistoryErrors.InvalidDateRange);
        RuleFor(filter => filter.FinancialStatus).Must(IsName<CourseOrderFinancialStatus>)
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(filter => filter.DeliveryStatus).Must(IsName<CourseOrderDeliveryStatus>)
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(filter => filter.SourceType).Must(IsName<CourseOrderSourceType>)
            .WithMessage(CourseOrderErrors.InvalidSourceType);
    }

    private static bool IsName<T>(string? value) where T : struct, Enum =>
        value is null || Enum.GetNames<T>().Contains(value, StringComparer.Ordinal);
}

public sealed class GetMyCourseOrdersQueryValidator : AbstractValidator<GetMyCourseOrdersQuery>
{
    public GetMyCourseOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .SetValidator(new CourseOrderFilterValidator());
    }
}

public sealed class GetAdminCourseOrdersQueryValidator : AbstractValidator<GetAdminCourseOrdersQuery>
{
    public GetAdminCourseOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .DependentRules(() =>
            {
                RuleFor(query => query.Filter.ToFilter()).SetValidator(new CourseOrderFilterValidator());
                RuleFor(query => query.Filter.BuyerExternalUserId)
                    .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
                    .WithMessage(PurchaseHistoryErrors.InvalidBuyer)
                    .MaximumLength(100).WithMessage(PurchaseHistoryErrors.InvalidBuyer);
            });
    }
}

public sealed class GetMyCourseOrderQueryValidator : AbstractValidator<GetMyCourseOrderQuery>
{
    public GetMyCourseOrderQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(CourseOrderId.IsValid).WithMessage(CourseOrderErrors.InvalidOrderId);
    }
}

public sealed class CourseOrderQueryHandlers(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCourseOrdersQuery, PurchasePage<CourseOrderListItem>>,
    IRequestHandler<GetMyCourseOrderQuery, CourseOrderDetails>,
    IRequestHandler<GetAdminCourseOrdersQuery, PurchasePage<AdminCourseOrderListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<CourseOrderListItem>> Handle(GetMyCourseOrdersQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var page = Page(CourseOrderMapping.Build(snapshot), request.Filter);
        return new PurchasePage<CourseOrderListItem>(page.Items.Select(row => row.Details.ToListItem()).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CourseOrderDetails> Handle(GetMyCourseOrderQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        return CourseOrderMapping.Build(snapshot).SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details
            ?? throw new NotFoundException(CourseOrderErrors.NotFound);
    }

    public async Task<PurchasePage<AdminCourseOrderListItem>> Handle(GetAdminCourseOrdersQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var snapshot = await reader.ReadAsync(null, request.Filter.BuyerExternalUserId, ct);
        var buyers = snapshot.Buyers.ToDictionary(buyer => buyer.Id);
        var page = Page(CourseOrderMapping.Build(snapshot), request.Filter.ToFilter());
        var items = page.Items.Select(row =>
        {
            var order = row.Details;
            var buyer = buyers[row.BuyerId];
            return new AdminCourseOrderListItem(order.OrderId, order.SourceType, order.CartId,
                order.CreatedAtUtc, order.UpdatedAtUtc, order.FinancialStatus, order.DeliveryStatus,
                order.FinalAmountInNoor, order.ItemCount, buyer.ExternalUserId,
                string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName);
        }).ToArray();
        return new PurchasePage<AdminCourseOrderListItem>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static PurchasePage<CourseOrderReadRow> Page(
        IReadOnlyList<CourseOrderReadRow> rows, CourseOrderFilter filter)
    {
        var filtered = rows.Where(row =>
            (!filter.FromUtc.HasValue || row.Details.CreatedAtUtc >= filter.FromUtc.Value.UtcDateTime) &&
            (!filter.ToUtc.HasValue || row.Details.CreatedAtUtc < filter.ToUtc.Value.UtcDateTime) &&
            (filter.FinancialStatus is null || row.Details.FinancialStatus == filter.FinancialStatus) &&
            (filter.DeliveryStatus is null || row.Details.DeliveryStatus == filter.DeliveryStatus) &&
            (filter.SourceType is null || row.Details.SourceType == filter.SourceType)).ToArray();
        var items = filtered.OrderByDescending(row => row.Details.CreatedAtUtc)
            .ThenByDescending(row => row.Details.OrderId, StringComparer.Ordinal)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new PurchasePage<CourseOrderReadRow>(items, filter.Page, filter.PageSize, filtered.Length);
    }
}
