using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Common.Generic;

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
