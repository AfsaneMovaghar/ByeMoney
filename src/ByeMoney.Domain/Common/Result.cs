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

public class Result<T> : Result
{
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of failed result.");
    private readonly T? _value;

    protected Result(bool isSuccess, T? value, string? errorMessage, ResultStatus status, string? errorCode = null)
        : base(isSuccess, errorMessage, status, errorCode)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultStatus.Success);
    public new static Result<T> Failure(string errorMessage, string? errorCode = null) => new(false, default, errorMessage, ResultStatus.Failure, errorCode);
    public new static Result<T> NotFound(string? errorMessage = null, string? errorCode = null) => new(false, default, errorMessage ?? DomainErrors.Common_EntityNotFound, ResultStatus.NotFound, errorCode);
    public new static Result<T> Conflict(string errorMessage, string? errorCode = null) => new(false, default, errorMessage, ResultStatus.Conflict, errorCode);
}

