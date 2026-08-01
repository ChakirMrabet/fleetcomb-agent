using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Updates.Queries;

public static class GetUpdateHistory
{
    public sealed record Query : IRequest<IReadOnlyList<UpdateAttempt>>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(ISoftwareStateStore software)
        : IRequestHandler<Query, IReadOnlyList<UpdateAttempt>>
    {
        public Task<IReadOnlyList<UpdateAttempt>> Handle(
            Query request, CancellationToken token) =>
            software.LoadUpdateAttemptsAsync(token);
    }
}
