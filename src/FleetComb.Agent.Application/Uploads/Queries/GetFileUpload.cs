using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Queries;

public static class GetFileUpload
{
    public sealed record Query(Guid UploadId, Guid AdapterId) : IRequest<FileUploadSession?>;
    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() { RuleFor(x => x.UploadId).NotEmpty(); RuleFor(x => x.AdapterId).NotEmpty(); }
    }
    public sealed class Handler(FileUploadService uploads) : IRequestHandler<Query, FileUploadSession?>
    {
        public async Task<FileUploadSession?> Handle(Query request, CancellationToken cancellationToken)
        {
            var value = await uploads.GetAsync(request.UploadId, cancellationToken);
            return value?.AdapterId == request.AdapterId ? value : null;
        }
    }
}
