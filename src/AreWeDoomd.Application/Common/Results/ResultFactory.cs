using System.Reflection;

namespace AreWeDoomd.Application.Common.Results;

public static class ResultFactory
{
    private static readonly MethodInfo ValidationMethod = typeof(ResultFactory)
        .GetMethod(nameof(CreateValidationGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static TResponse CreateValidation<TResponse>(IReadOnlyCollection<ValidationError> validationErrors)
        where TResponse : IResult
    {
        var responseType = typeof(TResponse);

        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
        {
            throw new InvalidOperationException(
                $"{responseType.Name} must be {typeof(Result<>).Name} to support validation failures.");
        }

        var valueType = responseType.GetGenericArguments()[0];
        var closedMethod = ValidationMethod.MakeGenericMethod(valueType);

        return (TResponse)closedMethod.Invoke(null, [validationErrors])!;
    }

    private static Result<TValue> CreateValidationGeneric<TValue>(IReadOnlyCollection<ValidationError> validationErrors)
    {
        return Result<TValue>.Validation(validationErrors);
    }
}
