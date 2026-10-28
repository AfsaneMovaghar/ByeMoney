namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed class AdminGrantGiftNoorResult
{
    public bool IsSuccess { get; private init; }
    public bool IsConflict { get; private init; }
    public bool HasInvalidUsers => InvalidUsers.Count > 0;
    public string? ErrorMessage { get; private init; }
    public AdminGrantGiftNoorResponse? Response { get; private init; }
    public IReadOnlyList<InvalidGiftUserDto> InvalidUsers { get; private init; } = [];

    public static AdminGrantGiftNoorResult Success(AdminGrantGiftNoorResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static AdminGrantGiftNoorResult Conflict(string message) =>
        new() { IsSuccess = false, IsConflict = true, ErrorMessage = message };

    public static AdminGrantGiftNoorResult WithInvalidUsers(IReadOnlyList<InvalidGiftUserDto> invalidUsers, string message) =>
        new() { IsSuccess = false, InvalidUsers = invalidUsers, ErrorMessage = message };

    public static AdminGrantGiftNoorResult Failure(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}

