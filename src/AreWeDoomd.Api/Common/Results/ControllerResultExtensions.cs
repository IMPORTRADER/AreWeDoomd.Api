using AreWeDoomd.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Common.Results;

public static class ControllerResultExtensions
{
    public static ActionResult<TResponse> ToActionResult<TValue, TResponse>(
        this ControllerBase controller,
        Result<TValue> result,
        Func<TValue, TResponse> mapSuccess)
    {
        if (result.IsSuccess)
        {
            return new ActionResult<TResponse>(controller.Ok(mapSuccess(result.Value!)));
        }

        return result.ErrorType switch
        {
            ErrorType.Validation => new ActionResult<TResponse>(
                controller.BadRequest(
                    new HttpValidationProblemDetails(
                        result.ValidationErrors
                            .GroupBy(error => error.PropertyName, error => error.ErrorMessage)
                            .ToDictionary(group => group.Key, group => group.Distinct().ToArray()))
                    {
                        Title = "Validation failed",
                        Detail = "One or more validation errors occurred.",
                        Status = StatusCodes.Status400BadRequest
                    })),
            ErrorType.NotFound => new ActionResult<TResponse>(
                controller.NotFound(CreateProblemDetails(result, StatusCodes.Status404NotFound, "Resource not found"))),
            ErrorType.Conflict => new ActionResult<TResponse>(
                controller.Conflict(CreateProblemDetails(result, StatusCodes.Status409Conflict, "Conflict"))),
            ErrorType.Failure => new ActionResult<TResponse>(
                controller.BadRequest(CreateProblemDetails(result, StatusCodes.Status400BadRequest, "Request failed"))),
            _ => new ActionResult<TResponse>(
                controller.StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ProblemDetails
                    {
                        Title = "Unexpected error",
                        Detail = "Unexpected error occurred.",
                        Status = StatusCodes.Status500InternalServerError
                    }))
        };
    }

    private static ProblemDetails CreateProblemDetails<TValue>(Result<TValue> result, int statusCode, string title)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = result.Error?.Message,
            Status = statusCode
        };
    }
}
