using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Status.Queries;

public static class GetLocalAgentStatus
{
    public sealed record Query : IRequest<LocalAgentStatus>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status)
        : IRequestHandler<Query, LocalAgentStatus>
    {
        public Task<LocalAgentStatus> Handle(Query request, CancellationToken token) =>
            status.GetLocalStatusAsync(token);
    }
}
