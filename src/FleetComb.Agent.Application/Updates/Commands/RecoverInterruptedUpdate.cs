using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Updates.Commands;

public static class RecoverInterruptedUpdate
{
    public sealed record Command : IRequest<UpdateStatus>;
    public sealed class Validator : AbstractValidator<Command>;

    public sealed class Handler(UpdateService updates) : IRequestHandler<Command, UpdateStatus>
    {
        public Task<UpdateStatus> Handle(Command request, CancellationToken token) =>
            updates.RecoverInterruptedAsync(token);
    }
}
