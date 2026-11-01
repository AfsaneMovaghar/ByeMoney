using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrderReceipt;

public sealed record GetMyCourseOrderReceiptQuery(string OrderId) : IRequest<CourseOrderReceipt>;
