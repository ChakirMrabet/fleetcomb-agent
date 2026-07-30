using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Enrollment.Commands;

public static class EnrollAgent
{
    public sealed record Command(Uri ServerUrl, string EnrollmentCode)
        : IRequest<EnrollmentResult>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.ServerUrl).NotNull().Must(uri => uri.IsAbsoluteUri);
            RuleFor(command => command.EnrollmentCode).NotEmpty();
        }
    }

    public sealed class Handler(EnrollmentService enrollment)
        : IRequestHandler<Command, EnrollmentResult>
    {
        public Task<EnrollmentResult> Handle(Command request, CancellationToken token) =>
            enrollment.EnrollAsync(request.ServerUrl, request.EnrollmentCode, token);
    }
}
