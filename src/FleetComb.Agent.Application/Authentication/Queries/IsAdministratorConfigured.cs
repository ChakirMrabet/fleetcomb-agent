using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using MediatR;

namespace FleetComb.Agent.Application.Authentication.Queries;

public static class IsAdministratorConfigured
{
    public sealed record Query : IRequest<bool>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(ILocalAdministratorStore administrator)
        : IRequestHandler<Query, bool>
    {
        public Task<bool> Handle(Query request, CancellationToken token) =>
            administrator.IsConfiguredAsync(token);
    }
}
