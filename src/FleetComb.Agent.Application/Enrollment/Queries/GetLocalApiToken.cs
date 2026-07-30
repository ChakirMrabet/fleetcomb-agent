using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Enrollment.Queries;

public static class GetLocalApiToken
{
    public sealed record Query : IRequest<string>;
    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(EnrollmentService enrollment) : IRequestHandler<Query, string>
    {
        public Task<string> Handle(Query request, CancellationToken token) =>
            enrollment.GetOrCreateLocalApiTokenAsync(token);
    }
}
