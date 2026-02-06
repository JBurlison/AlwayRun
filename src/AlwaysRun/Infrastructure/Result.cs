namespace AlwaysRun.Infrastructure;

/// <summary>
/// Result pattern for operations that can fail with expected errors.
/// </summary>
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public static implicit operator bool(Result result) => result.IsSuccess;
}

/// <summary>
/// Result pattern with a value for operations that can fail with expected errors.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public static implicit operator bool(Result<T> result) => result.IsSuccess;

    /// <summary>
    /// Maps the result value if successful.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess && Value is not null
            ? Result<TOut>.Success(mapper(Value))
            : Result<TOut>.Failure(Error ?? "Unknown error");

    /// <summary>
    /// Converts to a non-generic Result, discarding the value.
    /// </summary>
    public Result ToResult() =>
        IsSuccess ? Result.Success() : Result.Failure(Error ?? "Unknown error");
}
