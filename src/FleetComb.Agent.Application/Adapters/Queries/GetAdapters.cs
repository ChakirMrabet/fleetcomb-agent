using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Queries;

public static class GetAdapters
{
    public sealed record Query : IRequest<IReadOnlyList<LocalAdapterIdentity>>;
    public sealed class Validator : AbstractValidator<Query>;
    public sealed class Handler(CustomerAdapterService adapters)
        : IRequestHandler<Query, IReadOnlyList<LocalAdapterIdentity>>
    {
        public Task<IReadOnlyList<LocalAdapterIdentity>> Handle(Query request, CancellationToken token) =>
            adapters.ListAsync(token);
    }
}
