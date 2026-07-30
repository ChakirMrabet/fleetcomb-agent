using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Persistence;
using Xunit;

namespace FleetComb.Agent.ArchitectureTests;

public sealed class EnrollmentResetTests
{
    [Fact]
    public async Task ResetMovesLocalStateIntoTimestampedBackup()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), $"fleetcomb-agent-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "agent-state.json"), "identity");
            await File.WriteAllTextAsync(
                Path.Combine(directory, "software-inventory.json"), "inventory");
            var reset = new FileAgentDataReset(new RegistrationStore(directory));

            var backup = await reset.ResetAsync(default);

            Assert.False(File.Exists(Path.Combine(directory, "agent-state.json")));
            Assert.Equal(
                "identity", await File.ReadAllTextAsync(
                    Path.Combine(backup, "agent-state.json")));
            Assert.Equal(
                "inventory", await File.ReadAllTextAsync(
                    Path.Combine(backup, "software-inventory.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RegistrationStore(string directory) : IAgentRegistrationStore
    {
        public string DataDirectory => directory;
        public Task EnsureWritableAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<AgentRegistration?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AgentRegistration?>(null);
        public Task SaveAsync(
            AgentRegistration registration, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
