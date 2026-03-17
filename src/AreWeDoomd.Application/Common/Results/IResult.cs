namespace AreWeDoomd.Application.Common.Results;

public interface IResult
{
    bool IsSuccess { get; }

    ErrorType? ErrorType { get; }

    Error? Error { get; }

    IReadOnlyCollection<ValidationError> ValidationErrors { get; }
}
