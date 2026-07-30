using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Enrollment.Queries;

public static class GetRegistration
{
    public sealed record Query : IRequest<AgentRegistration?>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(IAgentRegistrationStore registrations)
        : IRequestHandler<Query, AgentRegistration?>
    {
        public Task<AgentRegistration?> Handle(Query request, CancellationToken token) =>
            registrations.LoadAsync(token);
    }
}
