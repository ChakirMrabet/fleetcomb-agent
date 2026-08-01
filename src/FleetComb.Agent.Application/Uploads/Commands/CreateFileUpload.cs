using System.Text.Json;
using FluentValidation;
using FleetComb.Agent.Domain;
using MediatR;

namespace FleetComb.Agent.Application.Uploads.Commands;

public static class CreateFileUpload
{
    public sealed record Command(Guid AdapterId, string LocalPath, string Category, string Schema,
        string ContentType, JsonElement Metadata, DateTimeOffset? CapturedAt)
        : IRequest<FileUploadSession>;
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AdapterId).NotEmpty(); RuleFor(x => x.LocalPath).NotEmpty();
            RuleFor(x => x.Category).Must(x => x is "scan" or "project" or "diagnostic" or "other");
            RuleFor(x => x.Schema).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ContentType).NotEmpty().MaximumLength(200);
        }
    }
    public sealed class Handler(FileUploadService uploads) : IRequestHandler<Command, FileUploadSession>
    {
        public Task<FileUploadSession> Handle(Command request, CancellationToken cancellationToken) =>
            uploads.CreateAsync(request.AdapterId, request.LocalPath, request.Category, request.Schema,
                request.ContentType, request.Metadata.GetRawText(), request.CapturedAt, cancellationToken);
    }
}
