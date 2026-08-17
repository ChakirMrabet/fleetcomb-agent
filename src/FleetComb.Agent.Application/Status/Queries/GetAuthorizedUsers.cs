using FleetComb.Agent.Domain;
using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Status.Queries;

public static class GetAuthorizedUsers
{
    public sealed record Query : IRequest<Result?>;

    public sealed record Result(
        Guid AssetId,
        string AssetSerialNumber,
        long PolicyRevision,
        DateTimeOffset GeneratedAt,
        DateTimeOffset LeaseExpiresAt,
        IReadOnlyList<DesiredAuthorizedUser> Users);

    public sealed class Validator : AbstractValidator<Query>;

    public sealed class Handler(AgentStatusService status, TimeProvider timeProvider)
        : IRequestHandler<Query, Result?>
    {
        public async Task<Result?> Handle(Query request, CancellationToken token)
        {
            var desired = await status.GetDesiredStateAsync(token);
            if (desired?.Authorization is null) return null;
            var now = timeProvider.GetUtcNow();
            var users = desired.Authorization.Users
                .Where(user => user.NotAfter > now)
                .OrderBy(user => user.Username, StringComparer.Ordinal)
                .ThenBy(user => user.MembershipId)
                .ToArray();
            return new Result(
                desired.AssetId,
                desired.Authorization.AssetSerialNumber,
                desired.Authorization.Revision,
                desired.Authorization.GeneratedAt,
                desired.Authorization.LeaseExpiresAt,
                users);
        }
    }
}
