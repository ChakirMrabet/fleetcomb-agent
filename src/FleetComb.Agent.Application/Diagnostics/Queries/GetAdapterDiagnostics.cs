using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Diagnostics.Queries;

public static class GetAdapterDiagnostics
{
    public sealed record Query : IRequest<Result>;
    public sealed record Result(
        string ProtocolVersion, IReadOnlyList<string> SupportedScopes,
        ProducerQueueStatus Queue, int RegisteredAdapters, int ActiveAdapters,
        int MaximumTelemetryPayloadBytes, int MaximumQueuedMessages);
    public sealed class Validator : AbstractValidator<Query>;
    public sealed class Handler(
        IProducerMessageStore messages, ILocalAdapterStore adapters)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken token)
        {
            var identities = await adapters.LoadAsync(token);
            return new Result(
                "1.0", Adapters.LocalAdapterScopes.All,
                await messages.GetStatusAsync(token), identities.Count,
                identities.Count(x => x.RevokedAt is null), 64 * 1024, 10_000);
        }
    }
}
