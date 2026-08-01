using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Queries;

public static class GetFileUploads
{
    public sealed record Query(Guid AdapterId) : IRequest<IReadOnlyList<FileUploadSession>>;
    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(x => x.AdapterId).NotEmpty();
    }
    public sealed class Handler(FileUploadService uploads)
        : IRequestHandler<Query, IReadOnlyList<FileUploadSession>>
    {
        public async Task<IReadOnlyList<FileUploadSession>> Handle(
            Query request, CancellationToken cancellationToken) =>
            (await uploads.ListAsync(cancellationToken))
                .Where(item => item.AdapterId == request.AdapterId).ToArray();
    }
}
