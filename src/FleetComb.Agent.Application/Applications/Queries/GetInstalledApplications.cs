using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Applications.Queries;

public static class GetInstalledApplications
{
    public sealed record Query : IRequest<IReadOnlyList<ApplicationObservation>>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status)
        : IRequestHandler<Query, IReadOnlyList<ApplicationObservation>>
    {
        public Task<IReadOnlyList<ApplicationObservation>> Handle(
            Query request, CancellationToken token) =>
            status.GetInventoryAsync(token);
    }
}
