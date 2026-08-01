using FluentValidation;
using FleetComb.Agent.Application.Abstractions;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Queries;

public static class GetFileUploadConfiguration
{
    public sealed record Query : IRequest<Result>;
    public sealed record Result(
        string InboxDirectory, IReadOnlyList<string> AllowedRoots, int ChunkSize,
        long MaximumFileBytes, IReadOnlyList<string> Categories);
    public sealed class Validator : AbstractValidator<Query>;
    public sealed class Handler(ILocalUploadFileProvider files) : IRequestHandler<Query, Result>
    {
        public Task<Result> Handle(Query request, CancellationToken cancellationToken) =>
            Task.FromResult(new Result(files.InboxDirectory, files.AllowedRoots,
                FileUploadService.ChunkSize, 100L * 1024 * 1024 * 1024,
                ["scan", "project", "diagnostic", "other"]));
    }
}
