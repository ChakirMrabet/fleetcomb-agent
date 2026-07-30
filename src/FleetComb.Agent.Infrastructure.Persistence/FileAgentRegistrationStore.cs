using System.Text.Json;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileAgentRegistrationStore : IAgentRegistrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string path;

    public FileAgentRegistrationStore()
    {
        DataDirectory = AgentDataDirectory.Resolve();
        path = Path.Combine(DataDirectory, "agent-state.json");
    }

    public string DataDirectory { get; }

    public async Task EnsureWritableAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataDirectory);
        var probePath = Path.Combine(DataDirectory, $".write-test-{Guid.NewGuid():N}");
        await using var stream = new FileStream(
            probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await stream.WriteAsync(new byte[] { 0 }, cancellationToken);
    }

    public async Task<AgentRegistration?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AgentRegistration>(
            stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(
        AgentRegistration registration, CancellationToken cancellationToken)
    {
        await EnsureWritableAsync(cancellationToken);
        await using (var stream = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(
                stream, registration, JsonOptions, cancellationToken);
        Secure(path);
    }

    internal static void Secure(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
