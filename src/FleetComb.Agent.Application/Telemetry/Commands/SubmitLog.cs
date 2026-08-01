using System.Text.Json;
using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Telemetry.Commands;

public static class SubmitLog
{
    public sealed record Command(
        Guid AdapterId, string Schema, string Severity, JsonElement Payload)
        : IRequest<ProducerMessage>;
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AdapterId).NotEmpty();
            RuleFor(x => x.Schema).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Severity).Must(x => x is "Trace" or "Debug" or "Info" or "Warning" or "Error" or "Critical");
        }
    }
    public sealed class Handler(ProducerMessageService messages)
        : IRequestHandler<Command, ProducerMessage>
    {
        public Task<ProducerMessage> Handle(Command request, CancellationToken token) =>
            messages.SubmitAsync(
                request.AdapterId, "log", request.Schema, request.Severity,
                request.Payload, token);
    }
}
