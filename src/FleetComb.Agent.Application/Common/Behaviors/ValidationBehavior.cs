using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(validator =>
                validator.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();
        if (failures.Length != 0) throw new ValidationException(failures);

        return await next();
    }
}
