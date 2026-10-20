using ByeMoney.Application.Modules.Purchases.History.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchaseReceipt;

public sealed record GetAdminCoursePurchaseReceiptQuery(Guid PurchaseId) : IRequest<PurchaseReceipt>;
