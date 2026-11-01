using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrderReceipt;

public sealed record GetAdminCourseOrderReceiptQuery(string OrderId) : IRequest<CourseOrderReceipt>;
