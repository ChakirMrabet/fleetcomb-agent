using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Status.Queries;

public static class GetDesiredState
{
    public sealed record Query : IRequest<DesiredState?>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status)
        : IRequestHandler<Query, DesiredState?>
    {
        public Task<DesiredState?> Handle(Query request, CancellationToken token) =>
            status.GetDesiredStateAsync(token);
    }
}
