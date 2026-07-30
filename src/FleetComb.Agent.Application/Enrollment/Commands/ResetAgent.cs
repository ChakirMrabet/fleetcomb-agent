using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Enrollment.Commands;

public static class ResetAgent
{
    public sealed record Command : IRequest<string>;
    public sealed class Validator : AbstractValidator<Command>;

    public sealed class Handler(AgentResetService reset) : IRequestHandler<Command, string>
    {
        public Task<string> Handle(Command request, CancellationToken token) =>
            reset.ResetAsync(token);
    }
}
