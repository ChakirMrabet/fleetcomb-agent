using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Commands;

public static class RecordAdapterHeartbeat
{
    public sealed record Command(Guid AdapterId) : IRequest<CustomerAdapterStatus?>;
    public sealed class Validator : AbstractValidator<Command>;

    public sealed class Handler(CustomerAdapterService adapters)
        : IRequestHandler<Command, CustomerAdapterStatus?>
    {
        public Task<CustomerAdapterStatus?> Handle(Command request, CancellationToken token) =>
            adapters.HeartbeatAsync(request.AdapterId, token);
    }
}
