using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrder;

public sealed record GetAdminCourseOrderQuery(string OrderId) : IRequest<AdminCourseOrderDetails>;
