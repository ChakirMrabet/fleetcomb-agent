using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using MediatR;

namespace FleetComb.Agent.Application.Authentication.Commands;

public static class SetAdministratorPassword
{
    public sealed record Command(string Password) : IRequest;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() =>
            RuleFor(command => command.Password).NotEmpty().MinimumLength(12);
    }

    public sealed class Handler(ILocalAdministratorStore administrator)
        : IRequestHandler<Command>
    {
        public async Task<Unit> Handle(Command request, CancellationToken token)
        {
            await administrator.SetPasswordAsync(request.Password, token);
            return Unit.Value;
        }
    }
}
