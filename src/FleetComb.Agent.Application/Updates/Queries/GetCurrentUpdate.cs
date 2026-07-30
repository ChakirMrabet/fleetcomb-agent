using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Updates.Queries;

public static class GetCurrentUpdate
{
    public sealed record Query : IRequest<UpdateStatus>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status)
        : IRequestHandler<Query, UpdateStatus>
    {
        public Task<UpdateStatus> Handle(Query request, CancellationToken token) =>
            status.GetUpdateStatusAsync(token);
    }
}
