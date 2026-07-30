using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Commands;

public static class RegisterAdapter
{
    public sealed record Command(
        string Name,
        string Version,
        IReadOnlyList<string> Capabilities) : IRequest<CustomerAdapterStatus>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
            RuleFor(command => command.Version).NotEmpty().MaximumLength(100);
            RuleFor(command => command.Capabilities).NotNull().Must(items => items.Count <= 100);
        }
    }

    public sealed class Handler(CustomerAdapterService adapters)
        : IRequestHandler<Command, CustomerAdapterStatus>
    {
        public Task<CustomerAdapterStatus> Handle(Command request, CancellationToken token) =>
            adapters.RegisterAsync(request.Name, request.Version, request.Capabilities, token);
    }
}
