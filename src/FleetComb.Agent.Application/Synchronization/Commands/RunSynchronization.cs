using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Synchronization.Commands;

public static class RunSynchronization
{
    public sealed record Command : IRequest;
    public sealed class Validator : AbstractValidator<Command>;

    public sealed class Handler(AgentSynchronizationService synchronization)
        : IRequestHandler<Command>
    {
        public async Task<Unit> Handle(Command request, CancellationToken token)
        {
            await synchronization.RunAsync(synchronized: null, token);
            return Unit.Value;
        }
    }
}
