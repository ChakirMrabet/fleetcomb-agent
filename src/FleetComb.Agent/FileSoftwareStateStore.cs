using System.Text.Json;

namespace FleetComb.Agent;

public sealed class FileSoftwareStateStore : ISoftwareStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string directory = AgentDataDirectory.Resolve();

    public Task<DesiredState?> LoadDesiredAsync(CancellationToken cancellationToken) =>
        ReadAsync<DesiredState>("desired-state.json", cancellationToken);

    public Task SaveDesiredAsync(DesiredState desired, CancellationToken cancellationToken) =>
        WriteAsync("desired-state.json", desired, cancellationToken);

    public async Task<IReadOnlyList<ApplicationObservation>> LoadInventoryAsync(
        CancellationToken cancellationToken) =>
        await ReadAsync<ApplicationObservation[]>("software-inventory.json", cancellationToken)
        ?? [];

    public async Task SaveObservationAsync(
        ApplicationObservation observation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadCoreAsync<ApplicationObservation[]>(
                "software-inventory.json", cancellationToken) ?? [];
            var updated = items
                .Where(item => item.ApplicationId != observation.ApplicationId)
                .Append(observation)
                .OrderBy(item => item.ApplicationId)
                .ToArray();
            await WriteCoreAsync("software-inventory.json", updated, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<UpdateStatus> LoadUpdateStatusAsync(CancellationToken cancellationToken) =>
        await ReadAsync<UpdateStatus>("update-status.json", cancellationToken)
        ?? UpdateStatus.Idle();

    public Task SaveUpdateStatusAsync(UpdateStatus status, CancellationToken cancellationToken) =>
        WriteAsync("update-status.json", status, cancellationToken);

    private async Task<T?> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await ReadCoreAsync<T>(fileName, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task WriteAsync<T>(
        string fileName, T value, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { await WriteCoreAsync(fileName, value, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task<T?> ReadCoreAsync<T>(
        string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private async Task WriteCoreAsync<T>(
        string fileName, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await using (var stream = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(
                stream, value, JsonOptions, cancellationToken);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
