namespace FleetComb.Agent.Infrastructure.Persistence;

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
        var localData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        return Path.Combine(localData, "FleetComb", "Agent");
    }
}
