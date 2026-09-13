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

public class Result<T> : Result
{
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of failed result.");
    private readonly T? _value;

    protected Result(bool isSuccess, T? value, string? errorMessage, ResultStatus status)
        : base(isSuccess, errorMessage, status)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultStatus.Success);
    public new static Result<T> Failure(string errorMessage) => new(false, default, errorMessage, ResultStatus.Failure);
    public new static Result<T> NotFound(string? errorMessage = null) => new(false, default, errorMessage ?? DomainErrors.Common_EntityNotFound, ResultStatus.NotFound);
    public new static Result<T> Conflict(string errorMessage) => new(false, default, errorMessage, ResultStatus.Conflict);
}

