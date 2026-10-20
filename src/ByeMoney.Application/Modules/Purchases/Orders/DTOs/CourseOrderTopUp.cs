namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderTopUp(
    Guid TopUpRequestId, string Status, decimal AmountNoor, decimal? AmountRial,
    decimal? RialPerNoorSnapshot, string ClientReferenceCode, DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc, string? GatewayResultKind, string? ReviewReasonCode);
