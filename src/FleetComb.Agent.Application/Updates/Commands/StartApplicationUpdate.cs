using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Updates.Commands;

public static class StartApplicationUpdate
{
    public sealed record Command(Guid ApplicationId) : IRequest<UpdateStatus>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.ApplicationId).NotEmpty();
    }

    public sealed class Handler(UpdateService updates)
        : IRequestHandler<Command, UpdateStatus>
    {
        public Task<UpdateStatus> Handle(Command request, CancellationToken token) =>
            updates.StartAsync(request.ApplicationId, token);
    }
}
