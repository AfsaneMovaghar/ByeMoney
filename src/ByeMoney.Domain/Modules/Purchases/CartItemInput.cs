namespace ByeMoney.Domain.Modules.Purchases;

public sealed record CartItemInput(
    string ExternalId,
    ProductItemType Type = ProductItemType.Course,
    int Quantity = 1);
