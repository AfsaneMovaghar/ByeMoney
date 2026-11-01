namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record AdminCourseOrderDetails(
    string BuyerExternalUserId, string? BuyerDisplayName, CourseOrderDetails Order);
