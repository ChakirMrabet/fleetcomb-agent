using FluentValidation;
using FleetComb.Agent.Application.Adapters;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Commands;

public static class RegisterAdapter
{
    public sealed record Command(
        string Name,
        string Version,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string> Scopes) : IRequest<LocalAdapterRegistration>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Version).NotEmpty().MaximumLength(100);
            RuleFor(command => command.Capabilities).NotNull().Must(items => items.Count <= 100);
            RuleFor(command => command.Scopes).NotNull().Must(items => items.Count <= 20);
            RuleForEach(command => command.Scopes)
                .Must(scope => LocalAdapterScopes.All.Contains(scope, StringComparer.Ordinal))
                .WithMessage("An unsupported local API scope was requested.");
        }
    }

    public sealed class Handler(CustomerAdapterService adapters)
        : IRequestHandler<Command, LocalAdapterRegistration>
    {
        public Task<LocalAdapterRegistration> Handle(Command request, CancellationToken token) =>
            adapters.RegisterAsync(
                request.Name, request.Version, request.Capabilities, request.Scopes, token);
    }
}
