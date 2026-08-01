using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Commands;

public static class ProcessPendingFileUpload
{
    public sealed record Command : IRequest;
    public sealed class Validator : AbstractValidator<Command>;
    public sealed class Handler(FileUploadService uploads) : IRequestHandler<Command>
    {
        public async Task<Unit> Handle(Command request, CancellationToken cancellationToken)
        {
            await uploads.ProcessPendingAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
