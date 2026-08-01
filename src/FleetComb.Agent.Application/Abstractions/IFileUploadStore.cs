using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IFileUploadStore
{
    Task<IReadOnlyList<FileUploadSession>> LoadAsync(CancellationToken token);
    Task<FileUploadSession?> GetAsync(Guid id, CancellationToken token);
    Task SaveAsync(FileUploadSession session, CancellationToken token);
}
