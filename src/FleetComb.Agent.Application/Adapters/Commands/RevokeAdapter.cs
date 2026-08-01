using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Adapters.Commands;

public static class RevokeAdapter
{
    public sealed record Command(Guid AdapterId) : IRequest<bool>;
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.AdapterId).NotEmpty();
    }
    public sealed class Handler(CustomerAdapterService adapters) : IRequestHandler<Command, bool>
    {
        public Task<bool> Handle(Command request, CancellationToken token) =>
            adapters.RevokeAsync(request.AdapterId, token);
    }
}
