using FluentValidation;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Commands;

public static class RetryFileUpload
{
    public sealed record Command(Guid UploadId, Guid AdapterId) : IRequest<bool>;
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() { RuleFor(x => x.UploadId).NotEmpty(); RuleFor(x => x.AdapterId).NotEmpty(); }
    }
    public sealed class Handler(FileUploadService uploads) : IRequestHandler<Command, bool>
    {
        public Task<bool> Handle(Command request, CancellationToken cancellationToken) =>
            uploads.RetryAsync(request.UploadId, request.AdapterId, cancellationToken);
    }
}
