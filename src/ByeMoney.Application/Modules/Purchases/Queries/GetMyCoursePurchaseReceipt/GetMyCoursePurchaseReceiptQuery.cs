using ByeMoney.Application.Modules.Purchases.History.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchaseReceipt;

public sealed record GetMyCoursePurchaseReceiptQuery(Guid PurchaseId) : IRequest<PurchaseReceipt>;
