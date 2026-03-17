using AreWeDoomd.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Common.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails CreateProblemDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => CreateValidationProblem(validationException),
            NotFoundException notFoundException => new ProblemDetails
            {
                Title = "Resource not found",
                Detail = notFoundException.Message,
                Status = StatusCodes.Status404NotFound
            },
            ArgumentException or InvalidOperationException => new ProblemDetails
            {
                Title = "Request failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            },
            _ => new ProblemDetails
            {
                Title = "Unexpected error",
                Detail = "Unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            }
        };
    }

    private static HttpValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(
                failure => failure.PropertyName,
                failure => failure.ErrorMessage)
            .ToDictionary(
                group => group.Key,
                group => group.Distinct().ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Title = "Validation failed",
            Detail = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };
    }
}
