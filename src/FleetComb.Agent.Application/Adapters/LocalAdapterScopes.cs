namespace FleetComb.Agent.Application.Adapters;

public static class LocalAdapterScopes
{
    public const string StatusRead = "status.read";
    public const string ConfigurationRead = "configuration.read";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string UpdatesRead = "updates.read";
    public const string UpdatesInstall = "updates.install";
    public const string TelemetryWrite = "telemetry.write";
    public const string UploadsWrite = "uploads.write";
    public const string EventsSubscribe = "events.subscribe";

    public static readonly IReadOnlyList<string> All =
    [
        StatusRead, ConfigurationRead, InventoryRead, InventoryWrite, UpdatesRead,
        UpdatesInstall, TelemetryWrite, UploadsWrite, EventsSubscribe
    ];
}
