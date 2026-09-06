using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Common;

public enum ResultStatus
{
    Success,
    NotFound,
    Failure,
    Conflict
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    public ResultStatus Status { get; }

    protected Result(bool isSuccess, string? errorMessage, ResultStatus status)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Status = status;
    }

    public static Result Success() => new(true, null, ResultStatus.Success);
    public static Result Failure(string errorMessage) => new(false, errorMessage, ResultStatus.Failure);
    public static Result NotFound(string? errorMessage = null) => new(false, errorMessage ?? DomainErrors.Common_EntityNotFound, ResultStatus.NotFound);
    public static Result Conflict(string errorMessage) => new(false, errorMessage, ResultStatus.Conflict);
}

