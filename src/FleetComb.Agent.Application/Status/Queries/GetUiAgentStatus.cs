using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Status.Queries;

public static class GetUiAgentStatus
{
    public sealed record Query : IRequest<UiAgentStatus>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status)
        : IRequestHandler<Query, UiAgentStatus>
    {
        public Task<UiAgentStatus> Handle(Query request, CancellationToken token) =>
            status.GetUiStatusAsync(token);
    }
}
