using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }
    public ResultStatus Status { get; }

    protected Result(bool isSuccess, string? errorMessage, ResultStatus status, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        Status = status;
    }

    public static Result Success() => new(true, null, ResultStatus.Success);
    public static Result Failure(string errorMessage, string? errorCode = null) => new(false, errorMessage, ResultStatus.Failure, errorCode);
    public static Result NotFound(string? errorMessage = null, string? errorCode = null) => new(false, errorMessage ?? DomainErrors.Common_EntityNotFound, ResultStatus.NotFound, errorCode);
    public static Result Conflict(string errorMessage, string? errorCode = null) => new(false, errorMessage, ResultStatus.Conflict, errorCode);
}
