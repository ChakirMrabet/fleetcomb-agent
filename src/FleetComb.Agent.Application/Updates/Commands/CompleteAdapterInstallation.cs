using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Updates.Commands;

public static class CompleteAdapterInstallation
{
    public sealed record Command(
        Guid ApplicationId,
        bool Succeeded,
        string Message) : IRequest<UpdateStatus>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.ApplicationId).NotEmpty();
            RuleFor(command => command.Message).MaximumLength(2000);
        }
    }

    public sealed class Handler(UpdateService updates)
        : IRequestHandler<Command, UpdateStatus>
    {
        public Task<UpdateStatus> Handle(Command request, CancellationToken token) =>
            updates.CompleteAdapterInstallAsync(
                request.ApplicationId, request.Succeeded, request.Message, token);
    }
}
