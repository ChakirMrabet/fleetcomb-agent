using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Commands;

public static class AcknowledgeConfiguration
{
    public sealed record Command(Guid AdapterId, long Revision) : IRequest<bool>;
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AdapterId).NotEmpty();
            RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        }
    }
    public sealed class Handler(CustomerAdapterService adapters) : IRequestHandler<Command, bool>
    {
        public Task<bool> Handle(Command request, CancellationToken token) =>
            adapters.AcknowledgeConfigurationAsync(request.AdapterId, request.Revision, token);
    }
}
