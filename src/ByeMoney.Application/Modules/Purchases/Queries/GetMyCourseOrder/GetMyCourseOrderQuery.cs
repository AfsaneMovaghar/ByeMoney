using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrder;

public sealed record GetMyCourseOrderQuery(string OrderId) : IRequest<CourseOrderDetails>;
