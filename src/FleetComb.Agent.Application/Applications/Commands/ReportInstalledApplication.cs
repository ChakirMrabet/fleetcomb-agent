using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Applications.Commands;

public static class ReportInstalledApplication
{
    public sealed record Command(
        Guid ApplicationId,
        Guid? SoftwareReleaseId,
        string Version) : IRequest;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.ApplicationId).NotEmpty();
            RuleFor(command => command.Version).NotEmpty().MaximumLength(100);
        }
    }

    public sealed class Handler(AgentStatusService status) : IRequestHandler<Command>
    {
        public async Task<Unit> Handle(Command request, CancellationToken token)
        {
            await status.ReportApplicationAsync(
                request.ApplicationId, request.SoftwareReleaseId, request.Version, token);
            return Unit.Value;
        }
    }
}
