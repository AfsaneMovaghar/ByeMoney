namespace ByeMoney.API.Contracts.Auth;

public record SyncUserResponse(Guid UserId, bool Success = true);

