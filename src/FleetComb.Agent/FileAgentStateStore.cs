using System.Text.Json;

namespace FleetComb.Agent;

public sealed class FileAgentStateStore : IAgentStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string path = Path.Combine(AgentDataDirectory.Resolve(), "agent-state.json");

    public async Task<AgentState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AgentState>(
            stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(AgentState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var stream = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

public static class AgentDataDirectory
{
    public static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable("FLEETCOMB_AGENT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FleetComb", "Agent");
        return "/var/lib/fleetcomb-agent";
    }
}
