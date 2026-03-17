namespace AreWeDoomd.Application.Common.Results;

public sealed class Result<TValue> : IResult
{
    private Result(
        bool isSuccess,
        TValue? value,
        ErrorType? errorType,
        Error? error,
        IReadOnlyCollection<ValidationError> validationErrors)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        Error = error;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }

    public TValue? Value { get; }

    public ErrorType? ErrorType { get; }

    public Error? Error { get; }

    public IReadOnlyCollection<ValidationError> ValidationErrors { get; }

    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue>(true, value, null, null, Array.Empty<ValidationError>());
    }

    public static Result<TValue> Validation(IReadOnlyCollection<ValidationError> validationErrors)
    {
        return new Result<TValue>(
            false,
            default,
            Results.ErrorType.Validation,
            new Error("validation.failed", "One or more validation errors occurred."),
            validationErrors);
    }

    public static Result<TValue> NotFound(string code, string message)
    {
        return new Result<TValue>(
            false,
            default,
            Results.ErrorType.NotFound,
            new Error(code, message),
            Array.Empty<ValidationError>());
    }

    public static Result<TValue> Conflict(string code, string message)
    {
        return new Result<TValue>(
            false,
            default,
            Results.ErrorType.Conflict,
            new Error(code, message),
            Array.Empty<ValidationError>());
    }

    public static Result<TValue> Failure(string code, string message)
    {
        return new Result<TValue>(
            false,
            default,
            Results.ErrorType.Failure,
            new Error(code, message),
            Array.Empty<ValidationError>());
    }
}
