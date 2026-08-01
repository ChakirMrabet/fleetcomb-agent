using System.Text.Json;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileUploadStore(IAgentRegistrationStore registrations) : IFileUploadStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { WriteIndented = true };
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<FileUploadSession>> LoadAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return await ReadCoreAsync(token); }
        finally { gate.Release(); }
    }

    public async Task<FileUploadSession?> GetAsync(Guid id, CancellationToken token) =>
        (await LoadAsync(token)).SingleOrDefault(item => item.Id == id);

    public async Task SaveAsync(FileUploadSession session, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var values = (await ReadCoreAsync(token)).Where(item => item.Id != session.Id)
                .Append(session).OrderByDescending(item => item.CreatedAt).Take(500).ToArray();
            Directory.CreateDirectory(registrations.DataDirectory);
            var path = Path.Combine(registrations.DataDirectory, "file-uploads.json");
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, values, Options, token);
            FileAgentRegistrationStore.Secure(path);
        }
        finally { gate.Release(); }
    }

    private async Task<FileUploadSession[]> ReadCoreAsync(CancellationToken token)
    {
        var path = Path.Combine(registrations.DataDirectory, "file-uploads.json");
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<FileUploadSession[]>(stream, Options, token) ?? [];
    }
}
