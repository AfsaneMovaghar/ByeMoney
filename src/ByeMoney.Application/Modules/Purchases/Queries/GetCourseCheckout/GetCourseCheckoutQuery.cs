using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCheckout;

public sealed record GetCourseCheckoutQuery(string ExternalCourseId) : IRequest<CourseCheckoutResponse>;
