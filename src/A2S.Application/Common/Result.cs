namespace A2S.Application.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public ErrorCode ErrorCode { get; }

    protected Result(bool isSuccess, string error, ErrorCode errorCode = ErrorCode.None)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException("Success result cannot have an error");
        }

        if (!isSuccess && string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException("Failure result must have an error");
        }

        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(string error, ErrorCode errorCode) => new(false, error, errorCode);

    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
    public static Result<T> Failure<T>(string error) => new(default!, false, error);
    public static Result<T> Failure<T>(string error, ErrorCode errorCode) => new(default!, false, error, errorCode);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public class Result<T> : Result
{
    public T Value { get; }

    protected internal Result(T value, bool isSuccess, string error, ErrorCode errorCode = ErrorCode.None)
        : base(isSuccess, error, errorCode)
    {
        Value = value;
    }
}
