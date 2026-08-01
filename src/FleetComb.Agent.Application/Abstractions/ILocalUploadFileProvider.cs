using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface ILocalUploadFileProvider
{
    Task<LocalUploadFile> InspectAsync(string path, CancellationToken token);
    Task<byte[]> ReadChunkAsync(
        LocalUploadFile file, int chunkIndex, int chunkSize, CancellationToken token);
    string InboxDirectory { get; }
    IReadOnlyList<string> AllowedRoots { get; }
}
