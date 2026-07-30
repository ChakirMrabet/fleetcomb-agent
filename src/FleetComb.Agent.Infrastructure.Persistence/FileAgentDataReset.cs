using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileAgentDataReset(IAgentRegistrationStore registrations)
    : IAgentDataReset
{
    private static readonly string[] StateFiles =
    [
        "agent-state.json",
        "desired-state.json",
        "software-inventory.json",
        "update-status.json",
        "local-administrator.json"
    ];

    public Task<string> ResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backupDirectory = Path.Combine(
            registrations.DataDirectory, "reset-backups",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        var existing = StateFiles
            .Select(fileName => Path.Combine(registrations.DataDirectory, fileName))
            .Where(File.Exists)
            .ToArray();
        if (existing.Length == 0) return Task.FromResult(backupDirectory);
        Directory.CreateDirectory(backupDirectory);
        foreach (var path in existing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(path, Path.Combine(backupDirectory, Path.GetFileName(path)));
        }
        return Task.FromResult(backupDirectory);
    }
}
