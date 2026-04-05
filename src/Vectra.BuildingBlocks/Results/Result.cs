using Vectra.BuildingBlocks.Errors;

namespace Vectra.BuildingBlocks.Results;

public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Task<Result> SuccessAsync() => Task.FromResult(Success());
    public static Result Failure(Error error) => new(false, error);
    public static Task<Result> FailureAsync(Error error) => Task.FromResult(Failure(error));
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, null)
    {
        Value = value;
    }

    private Result(Error error) : base(false, error)
    {
    }

    public static Result<T> Success(T value) => new(value);
    public static Task<Result<T>> SuccessAsync(T value) => Task.FromResult(Success(value));
    public static new Result<T> Failure(Error error) => new(error);
    public static new Task<Result<T>> FailureAsync(Error error) => Task.FromResult(Failure(error));

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}