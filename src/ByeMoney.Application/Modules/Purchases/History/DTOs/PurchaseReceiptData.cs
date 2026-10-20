using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseReceiptData(CoursePurchase Purchase, CourseCart? Cart);
