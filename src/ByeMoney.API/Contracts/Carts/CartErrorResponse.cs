namespace ByeMoney.API.Contracts.Carts;

public sealed record CartErrorResponse(
    string Code,
    string Error);
