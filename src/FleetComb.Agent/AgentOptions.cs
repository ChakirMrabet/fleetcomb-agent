namespace FleetComb.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "FleetComb";
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public int HeartbeatIntervalSeconds { get; set; } = 60;
}
