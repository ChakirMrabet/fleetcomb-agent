using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using MediatR;

namespace FleetComb.Agent.Application.Authentication.Queries;

public static class VerifyAdministratorPassword
{
    public sealed record Query(string Password) : IRequest<bool>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(query => query.Password).NotEmpty();
    }

    public sealed class Handler(ILocalAdministratorStore administrator)
        : IRequestHandler<Query, bool>
    {
        public Task<bool> Handle(Query request, CancellationToken token) =>
            administrator.VerifyPasswordAsync(request.Password, token);
    }
}
