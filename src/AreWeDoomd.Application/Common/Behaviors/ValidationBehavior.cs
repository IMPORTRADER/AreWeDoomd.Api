using FluentValidation;
using MediatR;
using AreWeDoomd.Application.Common.Results;

namespace AreWeDoomd.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            return ResultFactory.CreateValidation<TResponse>(
                failures
                    .Select(failure => new ValidationError(failure.PropertyName, failure.ErrorMessage))
                    .ToArray());
        }

        return await next();
    }
}
